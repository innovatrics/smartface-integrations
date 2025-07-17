using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public class OccupancyActivityTracker
    {
        private class StreamState
        {
            public DateTime LastEventTime { get; set; }
            public string? LastEventId { get; set; }

            public bool IsInDebounceWindow(TimeSpan window) =>
                DateTime.UtcNow - LastEventTime < window;
        }

        private readonly ILogger _logger;
        private readonly GoogleCalendarService _calendarService;
        private readonly TimeSpan _debounceWindow = TimeSpan.FromMinutes(30);
        private readonly ConcurrentDictionary<string, StreamState> _states = new();

        public OccupancyActivityTracker(
            ILogger logger,
            GoogleCalendarService calendarService,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
        }

        public async Task HandleOccupancyChangeAsync(string streamGroupName, string calendarId, bool isOccupied)
        {
            if (!isOccupied)
            {
                _logger.Information("Occupancy ended in group {Group}, no calendar action needed.", streamGroupName);
                return;
            }

            var now = DateTime.UtcNow;
            var state = _states.GetOrAdd(streamGroupName, _ => new StreamState());

            if (state.IsInDebounceWindow(_debounceWindow))
            {
                _logger.Information("{Group} is in debounce window (until {Until}), skipping calendar event.", streamGroupName, state.LastEventTime.Add(_debounceWindow));
                return;
            }

            var start = now;
            var end = start.Add(_debounceWindow);

            var overlappingEvents = await _calendarService.GetOverlappingEventsAsync(
                calendarId,
                start.AddMinutes(-2),
                end.AddMinutes(2)
            );

            var matching = overlappingEvents.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.Summary) &&
                e.Summary.Contains(streamGroupName, StringComparison.OrdinalIgnoreCase));

            if (matching != null)
            {
                _logger.Information("Existing calendar event found for {Group}, skipping creation.", streamGroupName);
                state.LastEventTime = now;
                return;
            }

            var attendees = new List<string>();

            foreach (var identification in identifications)
            {
                attendees.Add(identification.Person.Email);
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

            _logger.Information("Created event for {Group} at {Start}, ID: {EventId}", streamGroupName, start.ToString("HH:mm"), eventId);
        }
    }
}
