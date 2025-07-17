using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SmartFace.GoogleCalendarsConnector.Models;
using System.Collections.Concurrent;
using System.Linq;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    /// <summary>
    /// Tracks stream group activity, debounces triggers, and manages Google Calendar event creation per group.
    /// </summary>
    public class StreamActivityTracker
    {
        private readonly ILogger _logger;
        private readonly GoogleCalendarService _calendarService;
        private readonly Dictionary<string, ActivityState> _groupStates = new();
        private readonly TimeSpan _debounceDuration;
        private readonly object _lock = new();

        // In-memory event cache (calendarId + time window as key)
        private readonly ConcurrentDictionary<string, GoogleCalendarEvent> _eventCache = new();
        private readonly TimeSpan _defaultCacheExpiration = TimeSpan.FromMinutes(30);
        private readonly int _maxCacheSize = 1000;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamActivityTracker"/> class.
        /// </summary>
        public StreamActivityTracker(ILogger logger, GoogleCalendarService calendarService, TimeSpan? debounceDuration = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
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

                var hasEvent = await HasOverlappingEventAsync(calendarId, now, end);
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

        // Cache logic
        private async Task<bool> HasOverlappingEventAsync(string calendarId, DateTime start, DateTime end)
        {
            CleanupExpiredEntries();
            var overlappingInCache = _eventCache.Values.Any(e => !e.IsExpired() && e.Start < end && e.End > start);
            if (overlappingInCache)
            {
                _logger.Debug("Cache hit for overlapping event in calendar {CalendarId} at {Start}", calendarId, start);
                return true;
            }

            if (_eventCache.Count >= _maxCacheSize)
            {
                RemoveOldestEntries(_maxCacheSize / 4);
            }

            _logger.Debug("Cache miss for overlapping event in calendar {CalendarId} at {Start}, checking API", calendarId, start);
            var overlappingEvents = await _calendarService.GetOverlappingEventsAsync(calendarId, start, end);
            foreach (var evt in overlappingEvents)
            {
                evt.ExpiresAt = DateTime.UtcNow.Add(_defaultCacheExpiration);
                _eventCache.TryAdd(GenerateCacheKey(calendarId, evt.Start, evt.End), evt);
            }
            return overlappingEvents.Any();
        }

        private string GenerateCacheKey(string calendarId, DateTime start, DateTime end)
        {
            var roundedStart = start.AddSeconds(-start.Second).AddMilliseconds(-start.Millisecond);
            var roundedEnd = end.AddSeconds(-end.Second).AddMilliseconds(-end.Millisecond);
            return $"{calendarId}_{roundedStart:yyyyMMddHHmm}_{roundedEnd:yyyyMMddHHmm}";
        }

        private void CleanupExpiredEntries()
        {
            var expiredKeys = _eventCache
                .Where(kvp => kvp.Value.IsExpired())
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in expiredKeys)
            {
                _eventCache.TryRemove(key, out _);
            }
            if (expiredKeys.Count > 0)
            {
                _logger.Debug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
            }
        }

        private void RemoveOldestEntries(int count)
        {
            var oldestEntries = _eventCache
                .OrderBy(kvp => kvp.Value.CreatedAt)
                .Take(count)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in oldestEntries)
            {
                _eventCache.TryRemove(key, out _);
            }
            _logger.Debug("Removed {Count} oldest cache entries", oldestEntries.Count);
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