using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Innovatrics.SmartFace.Integrations.Shared.SmartFaceRestApiClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.GoogleCalendarsConnector.Models;
using SmartFace.GoogleCalendarsConnector.Service;

namespace SmartFace.GoogleCalendarsConnector.Service
{
    public class QueueProcessingService
    {
        private readonly int MAX_PARALLEL_BLOCKS = 4;

        private readonly ILogger _logger;
        private readonly GoogleCalendarService _googleCalendarService;
        private readonly StreamGroupTracker _streamGroupTracker;

        private ActionBlock<StreamGroupAggregation> _actionBlock;

        public QueueProcessingService(
            ILogger logger,
            IConfiguration configuration,
            StreamGroupTracker streamGroupTracker,
            GoogleCalendarService googleCalendarService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamGroupTracker = streamGroupTracker ?? throw new ArgumentNullException(nameof(streamGroupTracker));
            _googleCalendarService = googleCalendarService ?? throw new ArgumentNullException(nameof(googleCalendarService));

            var config = configuration.GetSection("Config").Get<Config>();

            if (config?.MaxParallelActionBlocks > 0)
            {
                MAX_PARALLEL_BLOCKS = config.MaxParallelActionBlocks;
            }
        }

        public void Start()
        {
            _actionBlock = new ActionBlock<StreamGroupAggregation>(async notification =>
            {
                try
                {
                    _streamGroupTracker.OnDataReceived(notification.StreamGroupName, notification.AveragePedestrians, notification.AverageFaces);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process message");
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = MAX_PARALLEL_BLOCKS
            });

            _streamGroupTracker.OnTrigger += async (groupName) =>
            {
                _logger.Information("Action triggered for group {GroupName}", groupName);

                await _googleCalendarService.CreateEventAsync(groupName);
            };
        }

        public async Task StopAsync()
        {
            _actionBlock.Complete();
            await _actionBlock.Completion;
        }

        public void ProcessNotification(StreamGroupAggregation notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            _actionBlock.Post(notification);
        }
    }
}
