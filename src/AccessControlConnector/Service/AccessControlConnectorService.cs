using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public class AccessControlConnectorService
    {
        public readonly int MaxParallelBlocks;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IBridgeService _bridgeService;
        private readonly AccessNotificationThrottler _accessNotificationThrottler;
        private ActionBlock<GrantedNotification> _grantedNotificationsActionBlock;
        private ActionBlock<DeniedNotification> _deniedNotificationsActionBlock;
        private ActionBlock<BlockedNotification> _blockedNotificationsActionBlock;

        public AccessControlConnectorService(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IBridgeService bridgeService,
            AccessNotificationThrottler accessNotificationThrottler
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));
            _accessNotificationThrottler = accessNotificationThrottler ?? throw new ArgumentNullException(nameof(accessNotificationThrottler));

            MaxParallelBlocks = configuration.GetValue<int>("Config:MaxParallelActionBlocks", 4);
        }

        public void Start()
        {
            _accessNotificationThrottler.OnGranted += async (mapping, notification) =>
            {
                await _bridgeService.ProcessGrantedNotificationAsync(mapping, notification);
            };

            _accessNotificationThrottler.OnDenied += async (mapping, notification) =>
            {
                await _bridgeService.ProcessDeniedNotificationAsync(mapping, notification);
            };

            _accessNotificationThrottler.OnBlocked += async (mapping, notification) =>
            {
                await _bridgeService.ProcessBlockedNotificationAsync(mapping, notification);
            };

            _grantedNotificationsActionBlock = new ActionBlock<GrantedNotification>(async notification =>
            {
                try
                {
                    await _accessNotificationThrottler.HandleGrantedAsync(notification);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process message");
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = MaxParallelBlocks
            });

            _deniedNotificationsActionBlock = new ActionBlock<DeniedNotification>(async notification =>
            {
                try
                {
                    await _accessNotificationThrottler.HandleDeniedAsync(notification);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process message");
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = MaxParallelBlocks
            });

            _blockedNotificationsActionBlock = new ActionBlock<BlockedNotification>(async notification =>
            {
                try
                {
                    await _accessNotificationThrottler.HandleBlockedAsync(notification);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process message");
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = MaxParallelBlocks
            });
        }

        public async Task StopAsync()
        {
            _grantedNotificationsActionBlock.Complete();
            await _grantedNotificationsActionBlock.Completion;
        }

        public void ProcessNotification(GrantedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            _grantedNotificationsActionBlock.Post(notification);
        }

        public void ProcessNotification(DeniedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            _deniedNotificationsActionBlock.Post(notification);
        }

        public void ProcessNotification(BlockedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            _blockedNotificationsActionBlock.Post(notification);
        }

        public async Task SendKeepAliveSignalAsync()
        {
            await _bridgeService.SendKeepAliveSignalAsync();
        }
    }
}
