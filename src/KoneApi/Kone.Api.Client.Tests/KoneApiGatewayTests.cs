using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Kone.Api.Gateway;
using Serilog;

namespace Kone.Api.Client.Tests
{
    public class KoneApiGatewayTests : IClassFixture<KoneInitFixture>
    {
        private readonly KoneApiGateWay _koneApiGateWay;
        private readonly KoneInitFixture _fixture;

        public KoneApiGatewayTests(KoneInitFixture fixture)
        {
            _fixture = fixture;
            _koneApiGateWay = new KoneApiGateWay(fixture.KoneBuildingApi, Log.Logger);
        }

        [Fact]
        public async Task Test_GateWay_Filtering_Multiple_Calls()
        {
            var debounceTime = TimeSpan.FromSeconds(2);
            var callsResult = new ConcurrentBag<bool>();

            var ab = new ActionBlock<int>(async (area) =>
            {
                var callAlreadyInProgress = await _koneApiGateWay.SendLandingCallToAreaIfNotInProgressAsync(
                    area, true, debounceTime, CancellationToken.None);

                callsResult.Add(callAlreadyInProgress);

                await Task.Delay(Random.Shared.Next(200, 500), CancellationToken.None);
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 10
            });

            for (int i = 0; i < 100; i++)
            {
                await ab.SendAsync(_fixture.TestAreaId2, CancellationToken.None);
            }

            ab.Complete();
            await ab.Completion;

            var callsAccepted = callsResult.Count(x => x);
            Assert.True(callsAccepted is >= 1 and < 5, $"{callsAccepted}");

            // wait debounce time
            await Task.Delay(debounceTime * 2, CancellationToken.None);

            var accepted = await _koneApiGateWay.SendLandingCallToAreaIfNotInProgressAsync(
                _fixture.TestAreaId2, true, debounceTime, CancellationToken.None);

            // next one should be accepted again
            Assert.True(accepted);
        }
    }
}
