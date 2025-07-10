using System;
using System.IO;
using System.Linq;
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
        private readonly StreamGroupMapping[] _streamGroupsMapping;
        private readonly CalendarCacheService _calendarCacheService;

        private ActionBlock<StreamGroupAggregation> _actionBlock;

        public QueueProcessingService(
            ILogger logger,
            IConfiguration configuration,
            StreamGroupTracker streamGroupTracker,
            GoogleCalendarService googleCalendarService,
            CalendarCacheService calendarCacheService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamGroupTracker = streamGroupTracker ?? throw new ArgumentNullException(nameof(streamGroupTracker));
            _googleCalendarService = googleCalendarService ?? throw new ArgumentNullException(nameof(googleCalendarService));
            _calendarCacheService = calendarCacheService ?? throw new ArgumentNullException(nameof(calendarCacheService));

            var config = configuration.GetSection("Config").Get<Config>();

            if (config?.MaxParallelActionBlocks > 0)
            {
                MAX_PARALLEL_BLOCKS = config.MaxParallelActionBlocks;
            }

            //_streamGroupsMapping = GetMappingFromConfig();
        }

        public void Start()
        {
            _actionBlock = new ActionBlock<StreamGroupAggregation>(notification =>
            {
                try
                {
                    _streamGroupTracker.OnDataReceived(notification);
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

                var calendarId = _streamGroupsMapping
                                        .Where(x => x.GroupName == groupName)
                                        .Select(x => x.CalendarId)
                                        .FirstOrDefault();

                if (calendarId == null)
                {
                    _logger.Warning("Calendar ID not found for group {GroupName}", groupName);
                    return;
                }

                var now = DateTime.Now;
                var start = now;
                var end = now.AddMinutes(30); // Assuming 30-minute meeting duration

                // Use cache to check for overlapping events
                _logger.Debug("Checking for overlapping events in cache for group {GroupName} with calendar {CalendarId}", groupName, calendarId);
                
                var hasOverlappingEvent = await _calendarCacheService.HasOverlappingEventAsync(
                    calendarId, 
                    start, 
                    end, 
                    async (calId, startTime, endTime) => await _googleCalendarService.HasOverlappingEventAsync(calId, startTime, endTime));

                if (hasOverlappingEvent)
                {
                    _logger.Warning("Overlapping event found for group {GroupName} in calendar {CalendarId}", groupName, calendarId);
                    return;
                }

                _logger.Debug("No overlapping events found, creating new event for group {GroupName} in calendar {CalendarId}", groupName, calendarId);

                await _googleCalendarService.CreateEventAsync(groupName, calendarId);                

                _logger.Information("Trigger finished for group {GroupName}", groupName);
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

        // private Dictionary<StreamGroupMapping, string> GetMappingFromConfig()
        // {
        //     var config = _configuration.GetSection("StreamGroupMapping").Get<StreamGroupMapping[]>();
        //     return config;
        // }
    }
}
