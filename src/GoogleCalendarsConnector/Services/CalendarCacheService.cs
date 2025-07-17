using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Linq;

using SmartFace.GoogleCalendarsConnector.Models;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public class CalendarCacheService
    {
        private readonly ConcurrentDictionary<string, GoogleCalendarEvent> _cache;
        private readonly ILogger _logger;
        private readonly TimeSpan _defaultCacheExpiration;
        private readonly int _maxCacheSize;
        private readonly GoogleCalendarService _googleCalendarService;

        public CalendarCacheService(
            ILogger logger,
            IConfiguration configuration,
            GoogleCalendarService googleCalendarService
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = new ConcurrentDictionary<string, GoogleCalendarEvent>();

            // Get cache configuration from appsettings
            _defaultCacheExpiration = TimeSpan.FromMinutes(
                configuration.GetValue("CalendarCache:ExpirationMinutes", 30));
            _maxCacheSize = configuration.GetValue("CalendarCache:MaxSize", 1000);

            _googleCalendarService = googleCalendarService ?? throw new ArgumentNullException(nameof(googleCalendarService));
        }

        public async Task<bool> HasOverlappingEventAsync(string calendarId, DateTime start, DateTime end)
        {
            // Check for any overlapping event in the cache for the given time range
            var overlappingInCache = _cache.Values.Any(e =>
                !e.IsExpired() &&
                e.Start < end && e.End > start // overlap condition
            );
            
            if (overlappingInCache)
            {
                _logger.Debug("Cache hit for overlapping event in calendar {CalendarId} at {Start}", calendarId, start);
                return true;
            }

            // Check if cache is full and remove oldest entries if necessary
            if (_cache.Count >= _maxCacheSize)
            {
                CleanupExpiredEntries();
                if (_cache.Count >= _maxCacheSize)
                {
                    RemoveOldestEntries(_maxCacheSize / 4); // Remove 25% of entries
                }
            }

            // Fetch from API
            _logger.Debug("Cache miss for overlapping event in calendar {CalendarId} at {Start}, checking API", calendarId, start);
            var overlappingEvents = await _googleCalendarService.GetOverlappingEventsAsync(calendarId, start, end);

            // Cache the fetched events
            foreach (var evt in overlappingEvents)
            {
                evt.ExpiresAt = DateTime.UtcNow.Add(_defaultCacheExpiration);
                _cache.TryAdd(GenerateCacheKey(calendarId, evt.Start, evt.End), evt);
            }

            // Re-check cache for overlap
            var foundAfterFetch = overlappingEvents.Any();
            return foundAfterFetch;
        }

        private string GenerateCacheKey(string calendarId, DateTime start, DateTime end)
        {
            // Create a cache key based on calendar ID and time range
            // Round to nearest minute to group similar time ranges
            var roundedStart = start.AddSeconds(-start.Second).AddMilliseconds(-start.Millisecond);
            var roundedEnd = end.AddSeconds(-end.Second).AddMilliseconds(-end.Millisecond);

            return $"{calendarId}_{roundedStart:yyyyMMddHHmm}_{roundedEnd:yyyyMMddHHmm}";
        }

        private void CleanupExpiredEntries()
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired())
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.Debug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
            }
        }

        private void RemoveOldestEntries(int count)
        {
            var oldestEntries = _cache
                .OrderBy(kvp => kvp.Value.CreatedAt)
                .Take(count)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldestEntries)
            {
                _cache.TryRemove(key, out _);
            }

            _logger.Debug("Removed {Count} oldest cache entries", oldestEntries.Count);
        }

        public void ClearCache()
        {
            _cache.Clear();
            _logger.Information("Calendar cache cleared");
        }

        public int GetCacheSize()
        {
            return _cache.Count;
        }
    }
}