using Kone.Api.Client.Clients.Models;

namespace Kone.Api.Client.Tests
{
    /// <summary>
    /// In-memory IKoneBuildingApi for unit tests: only PlaceLandingCallWithPositionUpdatesAsync is implemented, by
    /// delegates that receive the call-state callback and the cancellation token. The n-th call uses the n-th delegate;
    /// the last one serves every later call.
    /// </summary>
    internal sealed class FakeKoneBuildingApi : IKoneBuildingApi
    {
        private readonly Func<Action<string>?, CancellationToken, Task>[] _placeLandingCalls;
        private int _calls;

        public FakeKoneBuildingApi(params Func<Action<string>?, CancellationToken, Task>[] placeLandingCalls)
        {
            _placeLandingCalls = placeLandingCalls;
        }

        public event Action<string>? MessageSend { add { } remove { } }
        public event Func<string, Task>? MessageReceived { add { } remove { } }

        public Task PlaceLandingCallWithPositionUpdatesAsync(int destinationAreaId, Action<string>? positionUpdated,
            bool isDirectionUp, CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _calls) - 1;
            return _placeLandingCalls[Math.Min(n, _placeLandingCalls.Length - 1)](positionUpdated, cancellationToken);
        }

        public Task<PingResponse> PingAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TopologyResponse> GetTopologyAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ActionsResponse> GetActionsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LiftPositionResponse> GetLiftPositionAsync(int liftId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LiftCallResponse> PlaceLandingCallAsync(int destinationAreaId, bool isDirectionUp,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LiftCallResponse> PlaceDestinationCallAsync(int sourceAreaId, int destinationAreaId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task PlaceDestinationCallWithPositionUpdatesAsync(int sourceAreaId, int destinationAreaId,
            Action<string>? positionUpdated, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
