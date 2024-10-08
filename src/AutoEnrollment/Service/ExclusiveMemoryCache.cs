using System;
using Microsoft.Extensions.Caching.Memory;

namespace SmartFace.AutoEnrollment.Service
{
    public class ExclusiveMemoryCache : IMemoryCache
    {
        internal const int CacheSyncTimeMs = 50;

        private readonly MemoryCache _cache = new(new MemoryCacheOptions
        {
            ExpirationScanFrequency = TimeSpan.FromMilliseconds(CacheSyncTimeMs)
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
