using Kone.Api.Client.Clients.Models;

namespace Kone.Api.Client
{
    public interface IKoneBuildingApi
    {
        Task<TopologyResponse> GetTopologyAsync(CancellationToken cancellationToken);
        Task<ActionsResponse> GetActionsAsync(CancellationToken cancellationToken);

        Task<LiftCallResponse> LandingCallAsync(int destinationAreaId, bool isDirectionUp, CancellationToken cancellationToken);
        Task<LiftCallResponse> DestinationCallAsync(int sourceAreaId, int destinationAreaId, CancellationToken cancellationToken);
    }
}
