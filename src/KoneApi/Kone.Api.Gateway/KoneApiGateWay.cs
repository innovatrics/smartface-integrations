using Kone.Api.Client;
using Kone.Api.Client.Clients.Extensions;
using Serilog;
using System.Collections.Concurrent;

namespace Kone.Api.Gateway
{
    public class KoneApiGateWay
    {
        private readonly IKoneBuildingApi _koneBuildingApi;
        private readonly ILogger _log;

        private readonly ConcurrentDictionary<int, Task> _activeLandingCalls = new();

        public KoneApiGateWay(IKoneBuildingApi koneBuildingApi, ILogger log)
        {
            _koneBuildingApi = koneBuildingApi ?? throw new ArgumentNullException(nameof(koneBuildingApi));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<bool> SendLandingCallToAreaIfNotInProgressAsync(
            int destinationAreaId,
            bool isDirectionUp,
            TimeSpan maxUpdateWaitTime,
            CancellationToken cancellationToken = default)
        {
            if (_activeLandingCalls.ContainsKey(destinationAreaId))
            {
                _log.Debug("Landing call skipped - already in progress for area {DestinationAreaId}", destinationAreaId);
                return false;
            }

            var landingCallTask = _koneBuildingApi.PlaceLandingCallUntilServedOrNoUpdateForAsync(
                destinationAreaId,
                isDirectionUp,
                maxUpdateWaitTime,
                cancellationToken);

            var registeredTask = _activeLandingCalls.GetOrAdd(destinationAreaId, landingCallTask);

            if (registeredTask != landingCallTask)
            {
                _log.Debug("Landing call skipped - already in progress for area {DestinationAreaId}", destinationAreaId);
                return false;
            }

            try
            {
                await landingCallTask;
                return true;
            }
            finally
            {
                _activeLandingCalls.TryRemove(destinationAreaId, out _);
            }
        }
    }
}