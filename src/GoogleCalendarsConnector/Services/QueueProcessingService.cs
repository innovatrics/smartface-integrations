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
using SmartFace.GoogleCalendarsConnector.Services;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public class QueueProcessingService
    {
        private readonly int _maxParallelBlocks = 4;

        private readonly ILogger _logger;
        private readonly GoogleCalendarService _googleCalendarService;
        private readonly StreamGroupTracker _streamGroupTracker;
        private readonly StreamGroupMapping[] _streamGroupsMapping;
        // Remove CalendarCacheService
        // private readonly CalendarCacheService _calendarCacheService;
        private readonly OccupancyActivityTracker _occupancyActivityTracker;

        private ActionBlock<StreamGroupAggregation> _actionBlock;

        public QueueProcessingService(
            ILogger logger,
            IConfiguration configuration,
            StreamGroupTracker streamGroupTracker,
            GoogleCalendarService googleCalendarService,
            // CalendarCacheService calendarCacheService,
            OccupancyActivityTracker occupancyActivityTracker
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamGroupTracker = streamGroupTracker ?? throw new ArgumentNullException(nameof(streamGroupTracker));
            _googleCalendarService = googleCalendarService ?? throw new ArgumentNullException(nameof(googleCalendarService));
            // _calendarCacheService = calendarCacheService ?? throw new ArgumentNullException(nameof(calendarCacheService));
            _occupancyActivityTracker = occupancyActivityTracker ?? throw new ArgumentNullException(nameof(occupancyActivityTracker));

            var config = configuration.GetSection("Config").Get<Config>();

            if (config?.MaxParallelActionBlocks > 0)
            {
                _maxParallelBlocks = config.MaxParallelActionBlocks;
            }
        }

        public void Start()
        {
            _streamGroupsMapping = GetMappingFromConfig();

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
                MaxDegreeOfParallelism = _maxParallelBlocks
            });

            _streamGroupTracker.OnOccupancyChanged += async (groupName, isOccupied) =>
            {
                _logger.Information("Occupancy changed for group {GroupName} to {IsOccupied}", groupName, isOccupied);

                try
                {
                    var calendarId = _streamGroupsMapping
                                            .Where(x => x.GroupName == groupName)
                                            .Select(x => x.CalendarId)
                                            .FirstOrDefault();

                    if (calendarId == null)
                    {
                        _logger.Warning("Calendar ID not found for group {GroupName}", groupName);
                        return;
                    }

                    switch (isOccupied)
                    {
                        case true:
                            await _occupancyActivityTracker.HandleGraphQLUpdateAsync(groupName, true);
                            break;

                        // case false:
                        //     await _occupancyActivityTracker.OnActivityAsync(groupName, false, calendarId);
                        //     break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process message");
                }
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

        private Dictionary<StreamGroupMapping, string> GetMappingFromConfig()
        {
            var config = _configuration.GetSection("StreamGroupMapping").Get<StreamGroupMapping[]>();
            return config;
        }
    }
}
