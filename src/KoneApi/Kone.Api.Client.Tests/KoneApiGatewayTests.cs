using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Kone.Api.Gateway;
using Serilog;
using Xunit.Abstractions;

namespace Kone.Api.Client.Tests
{
    public class KoneApiGatewayTests : IClassFixture<KoneInitFixture>
    {
        private readonly KoneApiGateWay _koneApiGateWay;
                private readonly ITestOutputHelper _output;
        private readonly KoneInitFixture _fixture;

        private readonly CancellationTokenSource _cts = new(10_0000);
        private CancellationToken CancellationToken => _cts.Token;

        public KoneApiGatewayTests(ITestOutputHelper output, KoneInitFixture fixture)
        {
            _fixture = fixture;
            _output = output ?? throw new ArgumentNullException(nameof(output));

            _koneApiGateWay = new KoneApiGateWay(fixture.KoneBuildingApi, Log.Logger);
        }

        [Fact]
        public async Task Test_GateWay_Filtering_Multiple_Calls()
        {
            var callsResult = new ConcurrentBag<bool>();

            var ab = new ActionBlock<int>(async (area) =>
            {
                var callAlreadyInProgress = await _koneApiGateWay.SendLandingCallToAreaIfNotInProgressAsync(
                    area, true, CancellationToken);

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
        }
    }
}
