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

            public bool IsInDebounceWindow(TimeSpan window) =>
                DateTime.UtcNow - LastEventTime < window;
        }

        private readonly ILogger _logger;
        private readonly GoogleCalendarService _calendarService;
        private readonly TimeSpan _eventWindow = TimeSpan.FromMinutes(30);
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

            // Step 1: Occupancy DETECTED
            if (isOccupied)
            {
                var overlappingEvents = await _calendarService.GetOverlappingEventsAsync(
                    calendarId,
                    now.AddMinutes(-2),
                    now.AddMinutes(2)
                );

                var matching = overlappingEvents.FirstOrDefault(e =>
                    !string.IsNullOrEmpty(e.Summary) &&
                    e.Summary.Contains(streamGroupName, StringComparison.OrdinalIgnoreCase));

                if (matching != null)
                {
                    // Optionally: extend the event if ending soon
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

                // Create new event
                var start = now;
                var end = start.Add(_eventWindow);
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

            // Step 2: Occupancy CLEARED
            else
            {
                if (!string.IsNullOrEmpty(state.LastEventId))
                {
                    _logger.Information("Ending event for {Group} (ID: {EventId}) due to cleared occupancy", streamGroupName, state.LastEventId);
                    await _calendarService.DeleteMeetingAsync(state.LastEventId);

                    state.LastEventId = null;
                    state.LastEventTime = DateTime.MinValue;
                    state.LastEventEndTime = DateTime.MinValue;
                }
                else
                {
                    _logger.Information("No active event for {Group} to end on occupancy cleared", streamGroupName);
                }
            }
        }
    }
}
