using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.GoogleCalendarsConnector.Models;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public class OccupancyActivityTracker
    {
        private class StreamState
        {
            public DateTime LastEventTime { get; set; }
            public string? LastEventId { get; set; }
            public DateTime LastEventEndTime { get; set; }
            public DateTime LastSeenOccupied { get; set; }
            public DateTime? LastSeenUnoccupied { get; set; }

            public bool IsInDebounceWindow(TimeSpan window) =>
                DateTime.UtcNow - LastEventTime < window;
        }

        private readonly ILogger _logger;
        private readonly GoogleCalendarService _calendarService;
        private readonly TimeSpan _eventWindow = TimeSpan.FromMinutes(30);
        private readonly TimeSpan _cooldownPeriod = TimeSpan.FromMinutes(5);
        private readonly ConcurrentDictionary<string, StreamState> _states = new();

        public OccupancyActivityTracker(
            ILogger logger,
            GoogleCalendarService calendarService,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
        }

        public async Task HandleOccupancyChangeAsync(string streamGroupName, string calendarId, bool isOccupied, IdentificationResult[] identifications)
        {
            var now = DateTime.UtcNow;
            var state = _states.GetOrAdd(streamGroupName, _ => new StreamState());

            if (isOccupied)
            {
                state.LastSeenOccupied = now;
                state.LastSeenUnoccupied = null;

                var start = now;
                var end = start.Add(_eventWindow);

                var overlappingEvents = await _calendarService.GetOverlappingEventsAsync(
                    calendarId,
                    start,
                    end
                );

                _logger.Information("Overlapping events found for {Group}: {@OverlappingEvents}", streamGroupName, overlappingEvents?.Select(e => e.Summary));

                var matching = overlappingEvents
                                    .Where(e =>
                                        !string.IsNullOrEmpty(e.Summary) &&
                                        e.Summary.Contains(streamGroupName, StringComparison.OrdinalIgnoreCase)
                                    )
                                    .FirstOrDefault();

                if (matching != null)
                {
                    var buffer = now.AddMinutes(5);
                    if (matching.End < buffer)
                    {
                        var newEnd = now.Add(_eventWindow);
                        await _calendarService.UpdateMeetingEndTimeAsync(calendarId, matching.EventId, newEnd);

                        _logger.Information("Extended event {EventId} for {Group} until {NewEnd}", matching.EventId, streamGroupName, newEnd);
                        state.LastEventEndTime = newEnd;
                    }
                    else
                    {
                        _logger.Information("Event {EventId} already covers current time for {Group}, skipping extension.", matching.EventId, streamGroupName);
                    }

                    state.LastEventTime = now;
                    state.LastEventId = matching.EventId;
                    return;
                }

                var attendees = new List<string>();

                if (identifications != null)
                {
                    foreach (var id in identifications)
                    {
                        var email = id?.MemberDetails?.Id;
                        if (!string.IsNullOrWhiteSpace(email))
                            attendees.Add(email);
                    }
                }

                var eventId = await _calendarService.CreateMeetingAsync(
                    calendarId,
                    $"Stream Group Activity: {streamGroupName}",
                    $"Activity detected in stream group {streamGroupName}",
                    "SmartFace System",
                    start,
                    end,
                    attendees.ToArray()
                );

                state.LastEventTime = now;
                state.LastEventId = eventId;
                state.LastEventEndTime = end;

                _logger.Information("Created event for {Group} at {Start}, ID: {EventId}", streamGroupName, start.ToString("HH:mm"), eventId);
            }
            else
            {
                // Occupancy OFF
                state.LastSeenUnoccupied ??= now;

                var timeSinceUnoccupied = now - state.LastSeenUnoccupied.Value;

                if (timeSinceUnoccupied < _cooldownPeriod)
                {
                    _logger.Information("Unoccupied for {Duration} of {Cooldown}, waiting before deleting for {Group}.", timeSinceUnoccupied, _cooldownPeriod, streamGroupName);
                    return;
                }

                if (!string.IsNullOrEmpty(state.LastEventId))
                {
                    _logger.Information("Deleting event for {Group} (ID: {EventId}) after cooldown.", streamGroupName, state.LastEventId);
                    await _calendarService.DeleteMeetingAsync(calendarId, state.LastEventId);

                    state.LastEventId = null;
                    state.LastEventTime = DateTime.MinValue;
                    state.LastEventEndTime = DateTime.MinValue;
                    state.LastSeenUnoccupied = null;
                }
                else
                {
                    _logger.Information("No active event to delete for {Group} after cooldown.", streamGroupName);
                }
            }
        }
    }
}
