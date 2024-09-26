using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.AutoEnrollment.Models;

namespace SmartFace.AutoEnrollment.Service
{
    public class DebouncingService
    {
        private readonly int _hardAbsoluteExpirationMs;

        private readonly ILogger _logger;
        private readonly ExclusiveMemoryCache _exclusiveMemoryCache;

        public DebouncingService(
            ILogger logger,
            IConfiguration configuration,
            ExclusiveMemoryCache exclusiveMemoryCache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exclusiveMemoryCache = exclusiveMemoryCache ?? throw new ArgumentNullException(nameof(exclusiveMemoryCache));

            _hardAbsoluteExpirationMs = configuration.GetValue<int>("Config:HardAbsoluteExpirationMs", 10000);
        }

        public void Block(Notification notification, StreamMapping mapping)
        {
            if (mapping.TrackletDebounceMs > 0)
            {
                Block(notification.TrackletId, mapping.TrackletDebounceMs.Value);
            }

            if (mapping.StreamDebounceMs > 0)
            {
                Block(notification.StreamId, mapping.StreamDebounceMs.Value);
            }

            if (mapping.GroupDebounceMs > 0 && mapping.StreamGroupId != null)
            {
                Block(mapping.StreamGroupId, mapping.GroupDebounceMs.Value);
            }
        }

        public bool IsBlocked(Notification notification, StreamMapping mapping)
        {
            if (mapping.TrackletDebounceMs > 0)
            {
                if (IsBlocked(notification.TrackletId, mapping.TrackletDebounceMs.Value))
                {
                    _logger.Information("Tracklet {tracklet} blocked for {ms}", notification.TrackletId, mapping.TrackletDebounceMs.Value);
                    return true;
                }
            }

            if (mapping.StreamDebounceMs > 0)
            {
                if (IsBlocked(notification.StreamId, mapping.StreamDebounceMs.Value))
                {
                    _logger.Information("Stream {stream} blocked for {ms}", notification.StreamId, mapping.StreamDebounceMs.Value);
                    return true;
                }
            }

            if (mapping.GroupDebounceMs > 0 && mapping.StreamGroupId != null)
            {
                if (IsBlocked(mapping.StreamGroupId, mapping.GroupDebounceMs.Value))
                {
                    _logger.Information("StreamGroupId {group} blocked for {ms}", mapping.StreamGroupId, mapping.GroupDebounceMs.Value);
                    return true;
                }
            }

            return false;
        }

        private bool IsBlocked(object key, long debounceMs)
        {
            lock (_exclusiveMemoryCache.Lock)
            {
                if (_exclusiveMemoryCache.TryGetValue(key, out DateTime timeStamp))
                {
                    if ((DateTime.UtcNow - timeStamp).TotalMilliseconds < debounceMs)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void Block(object key, long debounceMs)
        {
            lock (_exclusiveMemoryCache.Lock)
            {
                var now = DateTime.UtcNow;

                var absoluteExpiration = now.AddMilliseconds(_hardAbsoluteExpirationMs);

                _exclusiveMemoryCache.Set(key, now, absoluteExpiration);

                _logger.Debug("Cached {key} up to {expire}", key, absoluteExpiration);
            }
        }
    }
}