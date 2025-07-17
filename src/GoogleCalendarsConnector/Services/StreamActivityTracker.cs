using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SmartFace.GoogleCalendarsConnector.Models;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    /// <summary>
    /// Tracks stream group activity, debounces triggers, and manages Google Calendar event creation per group.
    /// </summary>
    public class StreamActivityTracker
    {
        private readonly ILogger _logger;
        private readonly GoogleCalendarService _calendarService;
        private readonly CalendarCacheService _cacheService;
        private readonly Dictionary<string, ActivityState> _groupStates = new();
        private readonly TimeSpan _debounceDuration;
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamActivityTracker"/> class.
        /// </summary>
        public StreamActivityTracker(ILogger logger, GoogleCalendarService calendarService, CalendarCacheService cacheService, TimeSpan? debounceDuration = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _debounceDuration = debounceDuration ?? TimeSpan.FromMinutes(30);
        }

        /// <summary>
        /// Call this when activity is detected for a group. Handles debouncing and event creation.
        /// </summary>
        /// <param name="groupName">The stream group name.</param>
        /// <param name="isPositive">True if activity is positive, false if negative.</param>
        /// <param name="calendarId">The Google Calendar ID for this group.</param>
        public async Task OnActivityAsync(string groupName, bool isPositive, string calendarId)
        {
            ActivityState state;
            lock (_lock)
            {
                if (!_groupStates.TryGetValue(groupName, out state))
                {
                    state = new ActivityState();
                    _groupStates[groupName] = state;
                }

                if (!isPositive)
                {
                    // Reset debounce on negative
                    state.IsActive = false;
                    state.LastEventId = null;
                    state.DebounceUntil = DateTime.MinValue;
                    state.DebounceCts?.Cancel();
                    _logger.Information("Activity negative for {GroupName}, debounce reset", groupName);
                    return;
                }

                // If still within debounce, do nothing
                if (state.IsActive && DateTime.UtcNow < state.DebounceUntil)
                {
                    _logger.Information("Debounce active for {GroupName}, skipping event creation", groupName);
                    return;
                }

                // Otherwise, check for overlapping event
                state.IsActive = true;
                state.DebounceUntil = DateTime.UtcNow.Add(_debounceDuration);
                state.DebounceCts?.Cancel();
                state.DebounceCts = new CancellationTokenSource();
            }

            // Run the debounced event handler outside the lock
            _ = HandleDebouncedEventAsync(groupName, calendarId, state, state.DebounceCts.Token);
        }

        private async Task HandleDebouncedEventAsync(string groupName, string calendarId, ActivityState state, CancellationToken token)
        {
            try
            {
                var now = DateTime.UtcNow;
                var end = now.Add(_debounceDuration);

                var hasEvent = await _cacheService.HasOverlappingEventAsync(calendarId, now, end);
                if (!hasEvent)
                {
                    // Create event
                    await _calendarService.CreateEventAsync(groupName, calendarId, new string[] { });
                    state.LastEventTime = now;
                    _logger.Information("Created new event for {GroupName}", groupName);
                }
                else
                {
                    _logger.Information("Overlapping event exists for {GroupName}, not creating new event", groupName);
                }

                // Wait for debounce period or cancellation
                try
                {
                    await Task.Delay(_debounceDuration, token);
                }
                catch (TaskCanceledException) { return; }

                // After debounce, if still active, repeat
                lock (_lock)
                {
                    if (state.IsActive)
                    {
                        state.DebounceUntil = DateTime.UtcNow.Add(_debounceDuration);
                        state.DebounceCts = new CancellationTokenSource();
                        _ = HandleDebouncedEventAsync(groupName, calendarId, state, state.DebounceCts.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in HandleDebouncedEventAsync for {GroupName}", groupName);
            }
        }

        /// <summary>
        /// Holds per-group activity and debounce state.
        /// </summary>
        private class ActivityState
        {
            public bool IsActive { get; set; } // true = positive, false = negative
            public DateTime LastEventTime { get; set; }
            public string LastEventId { get; set; }
            public DateTime DebounceUntil { get; set; }
            public CancellationTokenSource DebounceCts { get; set; }
        }
    }
} 