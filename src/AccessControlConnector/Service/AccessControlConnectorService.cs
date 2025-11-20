using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public class AccessControlConnectorService
    {
        public readonly int Parallelism;
        private readonly ILogger _logger;
        private readonly IBridgeService _bridgeService;
        private ActionBlock<GrantedNotification> _actionBlock;

        public AccessControlConnectorService(
            ILogger logger,
            IConfiguration configuration,
            IBridgeService bridgeService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));

            Parallelism = configuration.GetValue("Config:MaxParallelActionBlocks", 1);
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
                MaxDegreeOfParallelism = Parallelism,
                BoundedCapacity = 100
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

            if (!_actionBlock.Post(notification))
            {
                _logger.Error("Action block did not accept granted notification");
            }
        }

        public async Task SendKeepAliveSignalAsync()
        {
            await _bridgeService.SendKeepAliveSignalAsync();
        }
    }
}
