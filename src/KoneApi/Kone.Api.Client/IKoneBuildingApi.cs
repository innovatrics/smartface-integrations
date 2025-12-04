using Kone.Api.Client.Clients.Models;

namespace Kone.Api.Client
{
    public interface IKoneBuildingApi
    {
        event Action<string>? MessageSend;
        event Func<string, Task>? MessageReceived;

        Task<PingResponse> PingAsync(CancellationToken cancellationToken);
        Task<TopologyResponse> GetTopologyAsync(CancellationToken cancellationToken);
        Task<ActionsResponse> GetActionsAsync(CancellationToken cancellationToken);

        Task<LiftPositionResponse> GetLiftPositionAsync(int liftId, CancellationToken cancellationToken);

        Task PlaceLandingCallWithPositionUpdatesAsync(int destinationAreaId,
            Action<string>? positionUpdated, bool isDirectionUp,
            CancellationToken cancellationToken);

        Task<LiftCallResponse> PlaceLandingCallAsync(int destinationAreaId, bool isDirectionUp,
            CancellationToken cancellationToken);

        Task<LiftCallResponse> PlaceDestinationCallAsync(int sourceAreaId, int destinationAreaId,
            CancellationToken cancellationToken);

        Task PlaceDestinationCallWithPositionUpdatesAsync(int sourceAreaId, int destinationAreaId,
            Action<string>? positionUpdated, CancellationToken cancellationToken);
    }
}
