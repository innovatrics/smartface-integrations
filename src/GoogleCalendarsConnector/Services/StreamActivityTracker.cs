using Google.Apis.Calendar.v3.Data;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SmartFace.GoogleCalendarsConnector.Services
{

    public class StreamActivityTracker
    {
        private class StreamState
        {
            public DateTime LastEventTime { get; set; }
            public string? LastEventId { get; set; }

            public bool IsInDebounceWindow(TimeSpan window) =>
                DateTime.UtcNow - LastEventTime < window;
        }

        private readonly TimeSpan _debounceWindow = TimeSpan.FromMinutes(30);
        private readonly ConcurrentDictionary<string, StreamState> _states = new();

        private readonly Func<string, Task<Event?>> _findExistingEvent;
        private readonly Func<string, DateTime, DateTime, Task<string>> _createEvent;

        public StreamActivityTracker(
            ILogger logger,
            Func<string, Task<Event?>> findExistingEvent,
            Func<string, DateTime, DateTime, Task<string>> createEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _findExistingEvent = findExistingEvent ?? throw new ArgumentNullException(nameof(findExistingEvent));
            _createEvent = createEvent ?? throw new ArgumentNullException(nameof(createEvent));
        }

        public async Task HandleGraphQLUpdateAsync(string streamGroupName, bool activityDetected)
        {
            if (!activityDetected)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var state = _states.GetOrAdd(streamGroupName, _ => new StreamState());

            if (state.IsInDebounceWindow(_debounceWindow))
            {
                _logger.Information("{streamGroupName} is still in debounce window, skipping...", streamGroupName);
                return;
            }

            var existing = await _findExistingEvent(streamGroupName);
            
            if (existing != null)
            {
                _logger.Information("Existing event found for {streamGroupName}, skipping creation.", streamGroupName);

                state.LastEventTime = now;
                state.LastEventId = existing.Id;
                return;
            }

            var start = now;
            var end = start.Add(_debounceWindow);
            var eventId = await _createEvent(streamGroupName, start, end);

            state.LastEventTime = now;
            state.LastEventId = eventId;

            _logger.Information("Created event for {streamGroupName} at {start:HH:mm}, ID: {eventId}", streamGroupName, start, eventId);
        }
    }

}