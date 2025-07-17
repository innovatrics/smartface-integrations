using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
        private readonly OccupancyActivityTracker _occupancyActivityTracker;

        private ActionBlock<StreamGroupAggregation> _actionBlock;

        public QueueProcessingService(
            ILogger logger,
            IConfiguration configuration,
            StreamGroupTracker streamGroupTracker,
            GoogleCalendarService googleCalendarService,
            OccupancyActivityTracker occupancyActivityTracker
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamGroupTracker = streamGroupTracker ?? throw new ArgumentNullException(nameof(streamGroupTracker));
            _googleCalendarService = googleCalendarService ?? throw new ArgumentNullException(nameof(googleCalendarService));
            _occupancyActivityTracker = occupancyActivityTracker ?? throw new ArgumentNullException(nameof(occupancyActivityTracker));

            var config = configuration.GetSection("Config").Get<Config>();

            if (config?.MaxParallelActionBlocks > 0)
            {
                _maxParallelBlocks = config.MaxParallelActionBlocks;
            }

            _streamGroupsMapping = configuration.GetSection("StreamGroupsMapping").Get<StreamGroupMapping[]>();

            if (_streamGroupsMapping == null)
            {
                _streamGroupsMapping = new StreamGroupMapping[] { };
            }

            _logger.Information("Stream groups mapping: {@StreamGroupsMapping}", _streamGroupsMapping);
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
                            await _occupancyActivityTracker.HandleOccupancyChangeAsync(groupName, calendarId, true);
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
    }
}
