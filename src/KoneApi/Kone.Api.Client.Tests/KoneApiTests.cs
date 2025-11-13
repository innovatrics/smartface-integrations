using Kone.Api.Client.Clients;
using ManagementApi;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Kone.Api.Client.Tests
{
    public class KoneApiTests : IClassFixture<KoneInitFixture>
    {
        private readonly KoneAuthApiClient _koneAuthApi;
        private readonly KoneBuildingApiClient _koneBuildingApi;

        private readonly ITestOutputHelper _output;
        private readonly KoneInitFixture _fixture;

        private readonly CancellationTokenSource _cts = new(5000);
        private CancellationToken CancellationToken => _cts.Token;

        public KoneApiTests(ITestOutputHelper output, KoneInitFixture fixture)
        {
            _fixture = fixture;
            _koneAuthApi = fixture.KoneAuthApi;
            _koneBuildingApi = fixture.KoneBuildingApi;
            _output = output ?? throw new ArgumentNullException(nameof(output));

            /*_koneBuildingApi.MessageReceived += KoneWs_MessageReceived;
            _koneBuildingApi.MessageSend += KoneWs_MessageSend;*/
        }

        [Fact]
        public async Task Test_Auth_Get_Access_Token()
        {
            var tokenResponse = await _koneAuthApi.GetAccessTokenAsync(KoneAuthApiClient.DefaultScope, CancellationToken.None);

            _output.WriteLine(JsonConvert.SerializeObject(tokenResponse, Formatting.Indented));

            Assert.NotNull(tokenResponse.Access_token);
            Assert.NotEmpty(tokenResponse.Access_token);
            Assert.True(tokenResponse.Expires_in > 0);
        }

        [Fact]
        public async Task Test_Auth_Get_Access_Token_Returns_Not_Authorized()
        {
            var ex = await Assert.ThrowsAnyAsync<ApiException>(() => _koneAuthApi.GetResourcesAsync("InvalidToken", CancellationToken));
            Assert.Equal(401, ex.StatusCode);
        }

        [Fact]
        public async Task Test_Auth_Get_Resources()
        {
            var tokenResponse = await _koneAuthApi.GetAccessTokenAsync(KoneAuthApiClient.DefaultScope, CancellationToken);
            var resources = await _koneAuthApi.GetResourcesAsync(tokenResponse.Access_token, CancellationToken);

            _output.WriteLine(JsonConvert.SerializeObject(resources, Formatting.Indented));
        }

        [Fact]
        [KoneTestCase(6, "Landing Call")]
        public async Task Test_Landing_Call_Up_Successful()
        {
            var landingCallResponse = await _koneBuildingApi.LandingCallAsync(
                _fixture.TestAreaId1,
                isDirectionUp: true,
                CancellationToken);

            _output.WriteLine(landingCallResponse);
        }

        [Fact]
        [KoneTestCase(6, "Landing Call")]
        public async Task Test_Landing_Call_Down_Successful()
        {
            var landingCallResponse = await _koneBuildingApi.LandingCallAsync(
                _fixture.TestAreaId2,
                isDirectionUp: false,
                CancellationToken);

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
    }
}