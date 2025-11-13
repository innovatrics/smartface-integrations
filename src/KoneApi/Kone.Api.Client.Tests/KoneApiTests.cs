using Kone.Api.Client.Clients;
using ManagementApi;
using Newtonsoft.Json;
using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kone.Api.Client.Clients.Models;
using Xunit.Abstractions;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Kone.Api.Client.Tests
{
    public class KoneApiTests(ITestOutputHelper output) : IAsyncLifetime
    {
        private const string ClientId = "75496e37-3a35-495b-a0d7-d1a143080886";
        private const string ClientSecret = "ff29f835abbc267aa9813f66ac235cd69bbe6b69dad1c7ff214a589fdf2a1145";

        private readonly ITestOutputHelper _output = output ?? throw new ArgumentNullException(nameof(output));

        private readonly KoneAuthApiClient _koneAuthApiClient = new(ClientId, ClientSecret);
        private KoneBuildingApiClient _koneBuildingApi;

        private const string GroupId = "1";
        private TopologyResponse _topology;

        private string _buildingId;
        private int _testAreaId1;
        private int _testAreaId2;

        /// <summary>
        /// Authenticate and initialize building topology.
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            var tokenResponse = await _koneAuthApiClient.GetAccessTokenAsync();
            var resources = await _koneAuthApiClient.GetResourcesAsync(tokenResponse.Access_token);
            _buildingId = resources.Buildings.First().Id;

            _koneBuildingApi = new KoneBuildingApiClient(Log.Logger, _koneAuthApiClient, _buildingId, GroupId);

            _topology = await _koneBuildingApi.GetTopologyAsync(CancellationToken.None);

            Assert.NotNull(_topology);
            Assert.NotNull(_topology.data);
            Assert.NotNull(_topology.data.groups);
            Assert.NotEmpty(_topology.data.groups);

            _testAreaId1 = _topology.data.destinations.First().area_id;
            _testAreaId2 = _topology.data.destinations.Last().area_id;

            _koneBuildingApi.MessageReceived += KoneWs_MessageReceived;
            _koneBuildingApi.MessageSend += KoneWs_MessageSend;
        }

        [Fact]
        public async Task Test_Get_Access_Token()
        {
            var tokenResponse = await _koneAuthApiClient.GetAccessTokenAsync();

            _output.WriteLine(JsonConvert.SerializeObject(tokenResponse, Formatting.Indented));

            Assert.NotNull(tokenResponse.Access_token);
            Assert.NotEmpty(tokenResponse.Access_token);
            Assert.True(tokenResponse.Expires_in > 0);
        }

        [Fact]
        public async Task Test_List_Resources()
        {
            var tokenResponse = await _koneAuthApiClient.GetAccessTokenAsync();
            var resources = await _koneAuthApiClient.GetResourcesAsync(tokenResponse.Access_token);

            _output.WriteLine(JsonConvert.SerializeObject(resources, Formatting.Indented));
        }

        [Fact]
        public async Task Test_List_Resources_Invalid_Token_Returns_Not_Authorized()
        {
            var ex = await Assert.ThrowsAnyAsync<ApiException>(() => _koneAuthApiClient.GetResourcesAsync("InvalidToken"));
            Assert.Equal(401, ex.StatusCode);
        }

        [Fact]
        public async Task Test_Landing_Call_Up_Successful()
        {
            var cts = new CancellationTokenSource(5000);

            var landingCallResponse = await _koneBuildingApi.LandingCallAsync(
                _testAreaId1,
                isDirectionUp: true,
                cts.Token);

            _output.WriteLine(landingCallResponse);
        }

        [Fact]
        public async Task Test_Landing_Call_Down_Successful()
        {
            var cts = new CancellationTokenSource(5000);

            var landingCallResponse = await _koneBuildingApi.LandingCallAsync(
                _testAreaId2,
                isDirectionUp: false,
                cts.Token);

            _output.WriteLine(landingCallResponse);
        }

        private Task KoneWs_MessageReceived(string message)
        {
            ArgumentNullException.ThrowIfNull(message);
            var jsonNode = JsonNode.Parse(message);

            if (jsonNode == null)
            {
                _output.WriteLine(message);
                return Task.CompletedTask;
            }

            string prettyJson = jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            _output.WriteLine("MESSAGE RECEIVED");
            _output.WriteLine(prettyJson);
            _output.WriteLine("--------------------------------");
            return Task.CompletedTask;
        }

        private void KoneWs_MessageSend(string message)
        {
            _output.WriteLine("MESSAGE SEND");
            _output.WriteLine(message);
            _output.WriteLine("--------------------------------");
        }

        public async Task DisposeAsync()
        {
            await using (_koneBuildingApi)
            {
                _koneBuildingApi.MessageReceived -= KoneWs_MessageReceived;
                _koneBuildingApi.MessageSend -= KoneWs_MessageSend;
            }
        }
    }
}