using Kone.Api.Client.Clients;
using ManagementApi;
using Newtonsoft.Json;
using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Kone.Api.Client.Tests
{
    public class KoneApiTests(ITestOutputHelper output)
    {
        private const string ClientId = "75496e37-3a35-495b-a0d7-d1a143080886";
        private const string ClientSecret = "ff29f835abbc267aa9813f66ac235cd69bbe6b69dad1c7ff214a589fdf2a1145";

        const string GroupId = "1"; //TODO: Why hardcoded?

        private readonly ITestOutputHelper _output = output ?? throw new ArgumentNullException(nameof(output));

        private readonly KoneApiClient _koneApiClient = new(ClientId, ClientSecret);

        [Fact]
        public async Task Test_Get_Access_Token()
        {
            var tokenResponse = await _koneApiClient.GetAccessTokenAsync();

            _output.WriteLine(JsonConvert.SerializeObject(tokenResponse, Formatting.Indented));

            Assert.NotNull(tokenResponse.Access_token);
            Assert.NotEmpty(tokenResponse.Access_token);
            Assert.True(tokenResponse.Expires_in > 0);
        }

        [Fact]
        public async Task Test_List_Resources()
        {
            var tokenResponse = await _koneApiClient.GetAccessTokenAsync();
            var resources = await _koneApiClient.GetResourcesAsync(tokenResponse.Access_token);

            _output.WriteLine(JsonConvert.SerializeObject(resources, Formatting.Indented));
        }

        [Fact]
        public async Task Test_List_Resources_Invalid_Token_Returns_Not_Authorized()
        {
            var ex = await Assert.ThrowsAnyAsync<ApiException>(() => _koneApiClient.GetResourcesAsync("InvalidToken"));
            Assert.Equal(401, ex.StatusCode);
        }

        [Fact]
        public async Task Test_Get_Building_Topology()
        {
            var tokenResponse = await _koneApiClient.GetAccessTokenAsync();
            var resources = await _koneApiClient.GetResourcesAsync(tokenResponse.Access_token);
            var building = resources.Buildings.First();

            var cts = new CancellationTokenSource(5000);

            var koneWs = new KoneWebSocketApiClient(Log.Logger, _koneApiClient, building.Id, GroupId);

            koneWs.MessageReceived += KoneWs_MessageReceived;
            koneWs.MessageSend += KoneWs_MessageSend;

            var topology = await koneWs.GetBuildingTopologyAsync(cts.Token);
            Assert.NotNull(topology);
            Assert.NotNull(topology.data);
            Assert.NotNull(topology.data.groups);
            Assert.NotEmpty(topology.data.groups);

            _output.WriteLine("Building Topology:");
        }

        [Fact]
        public async Task Test_Landing_Call_Successful()
        {
            var tokenResponse = await _koneApiClient.GetAccessTokenAsync();
            var resources = await _koneApiClient.GetResourcesAsync(tokenResponse.Access_token);
            var building = resources.Buildings.First();

            var cts = new CancellationTokenSource(5000);

            var koneWs = new KoneWebSocketApiClient(Log.Logger, _koneApiClient, building.Id, GroupId);

            koneWs.MessageReceived += KoneWs_MessageReceived;
            koneWs.MessageSend += KoneWs_MessageSend;

            var response = await koneWs.CallLiftToAreaAsync(landingAreaId: 9, cts.Token);
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
    }
}