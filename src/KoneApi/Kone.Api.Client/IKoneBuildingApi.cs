using Kone.Api.Client.Clients.Models;

namespace Kone.Api.Client
{
    public interface IKoneBuildingApi
    {
        Task<TopologyResponse> GetTopologyAsync(CancellationToken cancellationToken);
        Task<ActionsResponse> GetActionsAsync(CancellationToken cancellationToken);

        Task<LiftCallResponse> PlaceLandingCallAsync(int destinationAreaId, bool isDirectionUp,
            CancellationToken cancellationToken);

        Task<LiftCallResponse> PlaceLandingCallAndWaitUntilServedAsync(int destinationAreaId,
            bool isDirectionUp,
            int maxWaitDurationSeconds,
            CancellationToken cancellationToken);

        Task<LiftCallResponse> PlaceDestinationCallAsync(int sourceAreaId, int destinationAreaId,
            CancellationToken cancellationToken);
    }
}
