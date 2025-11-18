using Kone.Api.Client.Clients.Models;

namespace Kone.Api.Client
{
    public interface IKoneBuildingApi
    {
        Task<TopologyResponse> GetTopologyAsync(CancellationToken cancellationToken);
        Task<ActionsResponse> GetActionsAsync(CancellationToken cancellationToken);

        Task<LiftPositionResponse> GetLiftPositionAsync(int liftId, CancellationToken cancellationToken);

        Task<LiftCallResponse> PlaceLandingCallAsync(int destinationAreaId, bool isDirectionUp,
            CancellationToken cancellationToken);

        Task<LiftCallResponse> PlaceLandingCallWithPositionUpdatesAsync(int destinationAreaId,
            Action<string>? positionUpdated, bool isDirectionUp, 
            CancellationToken cancellationToken);

        Task<LiftCallResponse> PlaceDestinationCallAsync(int sourceAreaId, int destinationAreaId,
            CancellationToken cancellationToken);
    }
}
