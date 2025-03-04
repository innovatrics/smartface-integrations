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
        private ActionBlock<GrantedNotification> _actionBlock;

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
            _actionBlock = new ActionBlock<GrantedNotification>(async notification =>
            {
                try
                {
                    await _bridgeService.ProcessGrantedNotificationAsync(notification);
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
            _actionBlock.Complete();
            await _actionBlock.Completion;
        }

        public void ProcessNotification(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            _actionBlock.Post(notification);
        }

        public async Task SendKeepAliveSignalAsync()
        {
            await _bridgeService.SendKeepAliveSignalAsync();
        }
    }
}
