using System.Collections.Concurrent;
using Kone.Api.Client.Clients;
using Kone.Api.Client.Clients.Generated;
using Kone.Api.Client.Exceptions;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kone.Api.Client.Clients.Models;
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
            var tokenResponse = await _koneAuthApi.GetDefaultAccessTokenAsync(CancellationToken);

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
            var tokenResponse = await _koneAuthApi.GetDefaultAccessTokenAsync(CancellationToken);
            var resources = await _koneAuthApi.GetResourcesAsync(tokenResponse.Access_token, CancellationToken);

            _output.WriteLine(JsonConvert.SerializeObject(resources, Formatting.Indented));
        }

        [Fact]
        [KoneTestCase(6, "Landing Call Up")]
        public async Task Test_Landing_Call_Up_Successful()
        {
            var landingCallResponse = await _koneBuildingApi.PlaceLandingCallAsync(
                destinationAreaId: _fixture.TestAreaId1,
                isDirectionUp: true,
                cancellationToken: CancellationToken);

            Assert.NotNull(landingCallResponse.data);
            Assert.True(landingCallResponse.data.success);
            Assert.True(landingCallResponse.data.request_id > 0);

            _output.WriteLine(landingCallResponse.ResponseMessageRaw);
        }

        [Fact]
        [KoneTestCase(6, "Landing Call Up - Invalid direction")]
        public async Task Test_Landing_Call_Up_Invalid_Direction()
        {
            var ex = await Assert.ThrowsAnyAsync<KoneCallException>(() => _koneBuildingApi.PlaceLandingCallAsync(
                destinationAreaId: _fixture.TestAreaId1,
                isDirectionUp: false,
                cancellationToken: CancellationToken));

            Assert.Equal(ex.Error, "INVALID_DIRECTION");

            _output.WriteLine(ex.JsonMessage);
        }

        [Fact]
        [KoneTestCase(6, "Landing Call")]
        public async Task Test_Landing_Call_Down_Successful()
        {
            var destination = _fixture.Destinations.Skip(2).First();

            var landingCallResponse = await _koneBuildingApi.PlaceLandingCallAsync(
                destinationAreaId: destination.area_id,
                isDirectionUp: false,
                cancellationToken: CancellationToken);

            Assert.NotNull(landingCallResponse.data);
            Assert.True(landingCallResponse.data.success);
            Assert.True(landingCallResponse.data.request_id > 0);

            _output.WriteLine(landingCallResponse.ResponseMessageRaw);
        }

        [Fact]
        [KoneTestCase(6, "Landing Call")]
        public async Task Test_Landing_Call_With_Position_Updates_Successful()
        {
            var positionUpdates = new ConcurrentBag<string>();

            var cts = new CancellationTokenSource(30_000);
            var positionUpdateReceivedTcs = new CancellationTokenSource(5000);
            positionUpdateReceivedTcs.Token.Register(() =>
            {
                // Stop waiting if after 5 seconds no position updates were received
                if (positionUpdates.IsEmpty)
                {
                    cts.Cancel();
                }
            });

            try
            {
                var destination = _fixture.Destinations.Skip(2).First();

                await _koneBuildingApi.PlaceLandingCallWithPositionUpdatesAsync(
                    destinationAreaId: destination.area_id,
                    isDirectionUp: false,
                    positionUpdated: m =>
                    {
                        positionUpdates.Add(m);
                        _output.WriteLine(m);
                    },
                    cancellationToken: cts.Token);
            }
            catch (OperationCanceledException) when (positionUpdates.IsEmpty)
            {

            }

            foreach (var positionUpdate in positionUpdates)
            {
                _output.WriteLine(positionUpdate);
            }
        }

        [Fact]
        public async Task Test_Lift_Position_Successful()
        {
            var lift = _fixture.Topology.data.groups.Single().lifts.First();
            var liftPosition = await _koneBuildingApi.GetLiftPositionAsync(lift.lift_id, CancellationToken);

            Assert.NotNull(liftPosition.data);

            Assert.True(DateTimeOffset.TryParse(liftPosition.data.time, out var parsed));
            Assert.True(parsed.Year == DateTime.UtcNow.Year);
            Assert.True(parsed.Month == DateTime.UtcNow.Month);
            Assert.True(parsed.Day == DateTime.UtcNow.Day);
            Assert.True(parsed.Hour == DateTime.UtcNow.Hour);

            Assert.Contains(LiftPositionData.Directions, d => d == liftPosition.data.coll);
            Assert.Contains(LiftPositionData.Directions, d => d == liftPosition.data.dir);
            Assert.Contains(LiftPositionData.MovingStates, d => d == liftPosition.data.moving_state);

            Assert.True(liftPosition.data.area > 0);
            Assert.True(liftPosition.data.cur > 0);
            Assert.True(liftPosition.data.adv > 0);
        }

        [Fact]
        [KoneTestCase(6, "Landing Call Down - Invalid direction")]
        public async Task Test_Landing_Call_Down_Invalid_Direction()
        {
            var ex = await Assert.ThrowsAnyAsync<KoneCallException>(() => _koneBuildingApi.PlaceLandingCallAsync(
                destinationAreaId: _fixture.TestAreaId2,
                isDirectionUp: true,
                cancellationToken: CancellationToken));

            Assert.Equal(ex.Error, "INVALID_DIRECTION");

            _output.WriteLine(ex.JsonMessage);
        }

        [Fact]
        public async Task Test_Destination_Call_Successful()
        {
            //TODO: Why these ids does not work ?
            var srcId = _fixture.SampleLiftAreaIds.First();
            var dstId = _fixture.SampleLiftAreaIds.Skip(1).First();

            var destinationCallResponse = await _koneBuildingApi.PlaceDestinationCallAsync(
                sourceAreaId: 3000,
                destinationAreaId: 5000,
                CancellationToken);

            Assert.NotNull(destinationCallResponse.data);
            Assert.True(destinationCallResponse.data.success);
            Assert.True(destinationCallResponse.data.request_id > 0);

            _output.WriteLine(destinationCallResponse.ResponseMessageRaw);
        }

        [Fact]
        public async Task Test_Destination_Call_Invalid()
        {
            //TODO: Why these ids does not work ?
            var srcId = _fixture.SampleLiftAreaIds.First();
            var dstId = _fixture.SampleLiftAreaIds.Skip(1).First();

            var ex = await Assert.ThrowsAnyAsync<KoneCallException>(() => _koneBuildingApi.PlaceDestinationCallAsync(
                sourceAreaId: 3000,
                destinationAreaId: 3000,
                CancellationToken));

            Assert.Equal(ex.Error, "SAME_SOURCE_AND_DEST_FLOOR");
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