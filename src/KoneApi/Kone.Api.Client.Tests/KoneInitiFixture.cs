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
        public required KoneBuildingApiClient KoneBuildingApi;

        public const string GroupId = "1";
        public required TopologyResponse Topology;
        public required ActionsResponse Actions;

        public required string BuildingId;

        public int TestAreaId1;
        public int TestAreaId2;

        public List<Destination> Destinations = [];

        public List<int> SampleLiftAreaIds = [];

        /// <summary>
        /// Authenticate and initialize building topology.
        /// </summary>
        /// <returns></returns>
        [KoneTestCase(0, "Building id can be retrieved")]
        [KoneTestCase(1, "Authentication successful")]
        [KoneTestCase(1, "Building config can be obtained")]
        [KoneTestCase(1, "Building actions can be obtained")]
        public async Task InitializeAsync()
        {
            var ct = new CancellationTokenSource(10_000).Token;

            var tokenResponse = await KoneAuthApi.GetDefaultAccessTokenAsync(ct);
            var resources = await KoneAuthApi.GetResourcesAsync(tokenResponse.Access_token, ct);

            BuildingId = resources.Buildings.First().Id;

            KoneBuildingApi = new KoneBuildingApiClient(Log.Logger, KoneAuthApi, BuildingId, GroupId);

            Topology = await KoneBuildingApi.GetTopologyAsync(ct);

            Assert.Equal(GroupId, Topology.groupId);

            Assert.NotNull(Topology);
            Assert.NotNull(Topology.data);
            Assert.NotNull(Topology.data.groups);
            Assert.NotEmpty(Topology.data.groups);

            Actions = await KoneBuildingApi.GetActionsAsync(ct);

            var landingCallUpAction = Actions.data.call_types.Single(x => x.name == "LdgCall UP");
            var landingCallDownAction = Actions.data.call_types.Single(x => x.name == "LdgCall DOWN");
            var destinationCallAction = Actions.data.call_types.Single(x => x.name == "DcsCall");

            Assert.Equal(KoneBuildingApiClient.LandingCallUpActionId, landingCallUpAction.action_id);
            Assert.Equal(KoneBuildingApiClient.LandingCallDownActionId, landingCallDownAction.action_id);
            Assert.Equal(KoneBuildingApiClient.DestinationCallActionId, destinationCallAction.action_id);

            Destinations = Topology.data.destinations;

            TestAreaId1 = Destinations.First().area_id;
            TestAreaId2 = Destinations.Last().area_id;

            SampleLiftAreaIds = GetSampleAreaIdsForOneLift(Topology);
        }

        private static List<int> GetSampleAreaIdsForOneLift(TopologyResponse topology)
        {
            var group = topology.data.groups.First();
            var lift = group.lifts.First(x => x.floors.Count > 2);
            return lift.floors.Select(x => x.lift_floor_id).ToList();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
