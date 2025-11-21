using Kone.Api.Client.Clients;
using Serilog;

namespace Kone.Api.Client
{
    public static class KoneDiagnostics
    {
        public static async Task LogInfoAsync(KoneAuthApiClient koneAuthApi,
            ILogger log,
            CancellationToken cancellationToken,
            bool fullDiagnostic,
            string groupId = "1",
            Action<KoneBuildingApiClient>? buildingApiModifier = null)
        {
            ArgumentNullException.ThrowIfNull(koneAuthApi);
            ArgumentNullException.ThrowIfNull(log);

            var tokenResponse = await koneAuthApi.GetDefaultAccessTokenAsync(cancellationToken);
            var resources = await koneAuthApi.GetResourcesAsync(tokenResponse.Access_token, cancellationToken);

            log.Information("KONE Resource Info: {@KoneResources}", resources);

            foreach (var building in resources.Buildings)
            {
                log.Information("Fetching building info for Building with Id {BuildingId}", building.Id);

                var buildingApi = new KoneBuildingApiClient(log, koneAuthApi, building.Id, groupId);
                buildingApiModifier?.Invoke(buildingApi);

                var pingResponse = buildingApi.PingAsync(cancellationToken);
                log.Information("KONE Building Ping Result: {@Ping}", pingResponse);

                if (fullDiagnostic)
                {
                    var topology = await buildingApi.GetTopologyAsync(cancellationToken);

                    log.Information("KONE Building Topology: {@BuildingTopology}", topology);

                    var actions = await buildingApi.GetActionsAsync(cancellationToken);

                    log.Information("KONE Building Actions: {@BuildingActions}", actions);
                }
            }
        }
    }
}
