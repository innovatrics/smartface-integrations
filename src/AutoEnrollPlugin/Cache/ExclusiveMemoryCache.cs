using System;
using Microsoft.Extensions.Caching.Memory;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class ExclusiveMemoryCache : IExclusiveMemoryCache
    {
        internal const int CACHE_SYNC_TIME_MS = 50;

        private readonly MemoryCache _cache = new(new MemoryCacheOptions
        {
            ExpirationScanFrequency = TimeSpan.FromMilliseconds(CACHE_SYNC_TIME_MS)
        });

        public object Lock => _cache;

        public ICacheEntry CreateEntry(object key)
        {
            return _cache.CreateEntry(key);
        }

        public void Remove(object key)
        {
            _cache.Remove(key);
        }

        public bool TryGetValue(object key, out object value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void Dispose()
        {
            _cache.Dispose();
        }
    }
}
