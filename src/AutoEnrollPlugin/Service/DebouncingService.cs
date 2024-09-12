using System;
using System.Linq;
using Serilog;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;

using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class DebouncingService : IDebouncingService
    {
        private readonly int HARD_SLIDING_EXPIRATION_MS;
        private readonly ILogger logger;
        private readonly IExclusiveMemoryCache exclusiveMemoryCache;

        public DebouncingService(
            ILogger logger,
            IConfiguration configuration,
            IExclusiveMemoryCache exclusiveMemoryCache
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.exclusiveMemoryCache = exclusiveMemoryCache ?? throw new ArgumentNullException(nameof(exclusiveMemoryCache));

            HARD_SLIDING_EXPIRATION_MS = configuration.GetValue<int>("Config:HardSlidingExpirationMs", 10000);
        }

        public bool IsBlocked(Notification notification, StreamMapping mapping)
        {
            if (mapping.TrackletDebounceMs > 0) 
            {
                if (IsBlocked(notification.TrackletId, mapping.TrackletDebounceMs.Value))
                {
                    this.logger.Information("Tracklet {tracklet} blocked for {ms}", notification.TrackletId, mapping.TrackletDebounceMs.Value);
                    return true;
                }
            }

            if (mapping.StreamDebounceMs > 0) 
            {
                if (IsBlocked(notification.StreamId, mapping.StreamDebounceMs.Value))
                {
                    this.logger.Information("Stream {stream} blocked for {ms}", notification.StreamId, mapping.StreamDebounceMs.Value);
                    return true;
                }
            }

            if (mapping.GroupDebounceMs > 0 && mapping.StreamGroupId != null) 
            {
                if (IsBlocked(mapping.StreamGroupId , mapping.GroupDebounceMs.Value))
                {
                    this.logger.Information("StreamGroupId {group} blocked for {ms}", mapping.StreamGroupId , mapping.GroupDebounceMs.Value);
                    return true;
                }
            }

            return false;
        }

        private bool IsBlocked(object key, long debounceMs)
        {
            lock (exclusiveMemoryCache.Lock)
            {
                if (exclusiveMemoryCache.TryGetValue(key, out DateTime timeStamp))
                {
                    if ((DateTime.UtcNow - timeStamp).TotalMilliseconds < debounceMs)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}