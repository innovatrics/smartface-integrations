using Kone.Api.Client;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace Kone.Api.Gateway
{
    public class KoneApiGateWay
    {
        private readonly IKoneBuildingApi _koneBuildingApi;
        private readonly ILogger _log;

        private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions
        {
            ExpirationScanFrequency = TimeSpan.FromMilliseconds(100)
        });

        public KoneApiGateWay(IKoneBuildingApi koneBuildingApi, ILogger log)
        {
            _koneBuildingApi = koneBuildingApi ?? throw new ArgumentNullException(nameof(koneBuildingApi));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task SendLandingCallIfNotRecentAsync(Guid memberId, int destinationAreaId, bool isDirectionUp, CancellationToken cancellationToken)
        {
            var key = $"{memberId}_{destinationAreaId}";

            if (_memoryCache.TryGetValue(key, out _))
            {
                _log.Debug("Skipping landing call for {Key} due to recent one already being called", key);
                return;
            }

            await _koneBuildingApi.LandingCallAsync(destinationAreaId, isDirectionUp, cancellationToken);

            _memoryCache.GetOrCreate(key, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromSeconds(8);
                return new object();
            });
        }
    }
}
