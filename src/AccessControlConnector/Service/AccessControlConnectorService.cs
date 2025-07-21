using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public class AccessControlConnectorService
    {
        public readonly int MaxParallelBlocks;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IBridgeService _bridgeService;
        private ActionBlock<GrantedNotification> _grantedNotificationsActionBlock;
        private ActionBlock<DeniedNotification> _deniedNotificationsActionBlock;
        private ActionBlock<BlockedNotification> _blockedNotificationsActionBlock;

        public AccessControlConnectorService(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IBridgeService bridgeService
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));

            MaxParallelBlocks = configuration.GetValue<int>("Config:MaxParallelActionBlocks", 4);
        }

        public void Start()
        {
            _grantedNotificationsActionBlock = new ActionBlock<GrantedNotification>(async notification =>
            {
                try
                {
                    await ProcessGrantedNotificationAsync(notification);
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
                    await ProcessDeniedNotificationAsync(notification);
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
                    await ProcessBlockedNotificationAsync(notification);
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

        private async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            var mappings = GetMappingsForNotification(notification.StreamId);
            foreach (var mapping in mappings)
            {
                await _bridgeService.ProcessGrantedNotificationAsync(mapping, notification);
            }
        }

        private async Task ProcessDeniedNotificationAsync(DeniedNotification notification)
        {
            var mappings = GetMappingsForNotification(notification.StreamId);
            foreach (var mapping in mappings)
            {
                await _bridgeService.ProcessDeniedNotificationAsync(mapping, notification);
            }
        }

        private async Task ProcessBlockedNotificationAsync(BlockedNotification notification)
        {
            var mappings = GetMappingsForNotification(notification.StreamId);
            foreach (var mapping in mappings)
            {
                await _bridgeService.ProcessBlockedNotificationAsync(mapping, notification);
            }
        }

        private AccessControlMapping[] GetMappingsForNotification(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                _logger.Warning("Invalid StreamId format: {streamId}", streamId);
                return Array.Empty<AccessControlMapping>();
            }
            var configMappings = _configuration.GetSection("AccessControlMapping").Get<AccessControlMapping[]>();
            return configMappings?.Where(m => m.StreamId == streamGuid).ToArray() ?? Array.Empty<AccessControlMapping>();
        }

        public async Task SendKeepAliveSignalAsync()
        {
            await _bridgeService.SendKeepAliveSignalAsync();
        }
    }
}
