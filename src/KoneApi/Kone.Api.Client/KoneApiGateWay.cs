using System.Collections.Concurrent;
using Kone.Api.Client.Clients.Extensions;
using Serilog;

namespace Kone.Api.Client
{
    public class KoneApiGateWay
    {
        private readonly IKoneBuildingApi _koneBuildingApi;
        private readonly ILogger _log;

        private readonly ConcurrentDictionary<int, Task> _activeLandingCalls = new();
        private readonly TimeSpan _maxCallDuration;

        /// <param name="maxCallDuration">Bounds a landing call whose caller passes a token that cannot be cancelled
        /// (default 60 s). When it fires, the caller gets an OperationCanceledException and the area is released.</param>
        public KoneApiGateWay(IKoneBuildingApi koneBuildingApi, ILogger log, TimeSpan? maxCallDuration = null)
        {
            _koneBuildingApi = koneBuildingApi ?? throw new ArgumentNullException(nameof(koneBuildingApi));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _maxCallDuration = maxCallDuration ?? TimeSpan.FromSeconds(60);

            if (_maxCallDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCallDuration), "maxCallDuration must be positive");
            }
        }

        /// <summary>
        /// Places a landing call to the area unless one is already in progress there, and waits until KONE reports it
        /// served, or until no call state has arrived within <paramref name="maxUpdateWaitTime"/>.
        /// </summary>
        /// <param name="cancellationToken">Bounds the wait once call states have arrived. A token that cannot be
        /// cancelled is replaced by the gateway's maxCallDuration, and maxUpdateWaitTime must then be shorter than it.
        /// A cancellable token without a timeout keeps the area in progress until KONE reports the call served.</param>
        /// <returns>false if skipped because a call to the area is already in progress, otherwise true.</returns>
        public async Task<bool> SendLandingCallToAreaIfNotInProgressAsync(
            int destinationAreaId,
            bool isDirectionUp,
            TimeSpan maxUpdateWaitTime,
            CancellationToken cancellationToken = default)
        {
            if (_activeLandingCalls.ContainsKey(destinationAreaId))
            {
                _log.Debug("Landing call skipped - already in progress for area {DestinationArea}", destinationAreaId);
                return false;
            }

            // Without a cancellable token, a call that is never reported served would hold its area forever
            if (!cancellationToken.CanBeCanceled && maxUpdateWaitTime >= _maxCallDuration)
            {
                throw new ArgumentOutOfRangeException(nameof(maxUpdateWaitTime),
                    "maxUpdateWaitTime must be shorter than maxCallDuration when the token cannot be cancelled");
            }

            var cap = cancellationToken.CanBeCanceled ? null : new CancellationTokenSource(_maxCallDuration);

            var landingCallTask = _koneBuildingApi.PlaceLandingCallUntilServedOrNoUpdateForAsync(
                destinationAreaId,
                isDirectionUp,
                maxUpdateWaitTime,
                cap?.Token ?? cancellationToken);

            var registeredTask = _activeLandingCalls.GetOrAdd(destinationAreaId, landingCallTask);

            if (registeredTask != landingCallTask)
            {
                // Lost the race to another call for this area after this call had already been placed. The cap is
                // deliberately left undisposed: it is the only bound on that call, which nobody awaits.
                _log.Debug("Landing call skipped - already in progress for area {DestinationArea}", destinationAreaId);
                return false;
            }

            try
            {
                var callStates = await landingCallTask;

                if (callStates.Length == 0)
                {
                    _log.Warning("Landing call to area {DestinationArea}: no call state from KONE within {MaxUpdateWaitTime}; whether the lift was called is unknown",
                        destinationAreaId, maxUpdateWaitTime);
                }

                return true;
            }
            finally
            {
                _activeLandingCalls.TryRemove(destinationAreaId, out _);
                cap?.Dispose();
            }
        }
    }
}