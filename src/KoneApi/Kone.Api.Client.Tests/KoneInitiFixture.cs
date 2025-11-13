using Kone.Api.Client.Clients;
using Kone.Api.Client.Clients.Models;
using Serilog;

namespace Kone.Api.Client.Tests
{
    public class KoneInitFixture : IAsyncLifetime
    {
        private const string ClientId = "75496e37-3a35-495b-a0d7-d1a143080886";
        private const string ClientSecret = "ff29f835abbc267aa9813f66ac235cd69bbe6b69dad1c7ff214a589fdf2a1145";

        public readonly KoneAuthApiClient KoneAuthApi = new(ClientId, ClientSecret);
        public KoneBuildingApiClient KoneBuildingApi;

        public const string GroupId = "1";
        public TopologyResponse Topology;
        public ActionsResponse Actions;

        public string BuildingId;

        public int TestAreaId1;
        public int TestAreaId2;

        /// <summary>
        /// Authenticate and initialize building topology.
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            var ct = new CancellationTokenSource(10_000).Token;

            var tokenResponse = await KoneAuthApi.GetAccessTokenAsync(KoneAuthApiClient.DefaultScope, ct);
            var resources = await KoneAuthApi.GetResourcesAsync(tokenResponse.Access_token, ct);
            BuildingId = resources.Buildings.First().Id;

            KoneBuildingApi = new KoneBuildingApiClient(Log.Logger, KoneAuthApi, BuildingId, GroupId);

            Topology = await KoneBuildingApi.GetTopologyAsync(ct);

            Assert.NotNull(Topology);
            Assert.NotNull(Topology.data);
            Assert.NotNull(Topology.data.groups);
            Assert.NotEmpty(Topology.data.groups);

            Actions = await KoneBuildingApi.GetActionsAsync(ct);

            TestAreaId1 = Topology.data.destinations.First().area_id;
            TestAreaId2 = Topology.data.destinations.Last().area_id;
        }

        public async Task DisposeAsync()
        {
            await using (KoneBuildingApi) { }
        }
    }
}
