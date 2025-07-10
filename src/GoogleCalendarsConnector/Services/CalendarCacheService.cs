using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Linq;

namespace SmartFace.GoogleCalendarsConnector.Service
{
    public class CalendarCacheService
    {
        private readonly ConcurrentDictionary<string, CalendarCacheEntry> _cache;
        private readonly ILogger _logger;
        private readonly TimeSpan _defaultCacheExpiration;
        private readonly int _maxCacheSize;

        public CalendarCacheService(ILogger logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = new ConcurrentDictionary<string, CalendarCacheEntry>();
            
            // Get cache configuration from appsettings
            _defaultCacheExpiration = TimeSpan.FromMinutes(
                configuration.GetValue("CalendarCache:ExpirationMinutes", 30));
            _maxCacheSize = configuration.GetValue("CalendarCache:MaxSize", 1000);
        }

        public async Task<bool> HasOverlappingEventAsync(string calendarId, DateTime start, DateTime end, Func<string, DateTime, DateTime, Task<bool>> checkFunction)
        {
            var cacheKey = GenerateCacheKey(calendarId, start, end);
            
            // Try to get from cache first
            if (_cache.TryGetValue(cacheKey, out var cachedEntry))
            {
                if (!cachedEntry.IsExpired())
                {
                    _logger.Debug("Cache hit for calendar {CalendarId} at {Start}", calendarId, start);
                    return cachedEntry.HasOverlappingEvent;
                }
                else
                {
                    // Remove expired entry
                    _cache.TryRemove(cacheKey, out _);
                }
            }

            // Check if cache is full and remove oldest entries if necessary
            if (_cache.Count >= _maxCacheSize)
            {
                CleanupExpiredEntries();
                
                // If still full, remove oldest entries
                if (_cache.Count >= _maxCacheSize)
                {
                    RemoveOldestEntries(_maxCacheSize / 4); // Remove 25% of entries
                }
            }

            // Perform the actual check
            _logger.Debug("Cache miss for calendar {CalendarId} at {Start}, checking API", calendarId, start);
            var hasOverlappingEvent = await checkFunction(calendarId, start, end);
            
            // Cache the result
            var newEntry = new CalendarCacheEntry
            {
                HasOverlappingEvent = hasOverlappingEvent,
                ExpiresAt = DateTime.UtcNow.Add(_defaultCacheExpiration)
            };
            
            _cache.TryAdd(cacheKey, newEntry);
            
            return hasOverlappingEvent;
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

        private class CalendarCacheEntry
        {
            public bool HasOverlappingEvent { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

            public bool IsExpired()
            {
                return DateTime.UtcNow > ExpiresAt;
            }
        }
    }
} 