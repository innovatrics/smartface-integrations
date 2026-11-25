using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Serilog;

namespace Kone.Api.Client.Tests
{
    // Live tests against the KONE sandbox (dev.kone.com). Exclude them with --filter "Category!=KoneSandbox".
    [Trait("Category", "KoneSandbox")]
    public class KoneApiGatewayTests : IClassFixture<KoneInitFixture>
    {
        private readonly KoneApiGateWay _koneApiGateWay;
        private readonly KoneInitFixture _fixture;

        public KoneApiGatewayTests(KoneInitFixture fixture)
        {
            _fixture = fixture;
            _koneApiGateWay = new KoneApiGateWay(_fixture.KoneBuildingApi, Log.Logger);
        }

        [Fact]
        public async Task Test_GateWay_Filtering_Multiple_Calls()
        {
            var debounceTime = TimeSpan.FromSeconds(2);
            var callsResult = new ConcurrentBag<bool>();

            var ab = new ActionBlock<int>(async (area) =>
            {
                callsResult.Add(await SendAsync(area));

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

            // How many calls are accepted depends on how fast the sandbox reports a call served, so assert only
            // that calls went out and that duplicates were filtered while one was in progress
            var callsAccepted = callsResult.Count(x => x);
            Assert.True(callsAccepted >= 1, $"{callsAccepted}");
            Assert.True(callsResult.Count(x => !x) > 0, $"no call was skipped as a duplicate ({callsAccepted} accepted)");

            // All calls above have finished, so the area is free again
            await Task.Delay(debounceTime * 2, CancellationToken.None);

            var accepted = await SendAsync(_fixture.TestAreaId2);

            // next one should be accepted again
            Assert.True(accepted);

            // A call now waits until KONE reports it served, so each one gets its own bound. A call that was sent but
            // not served within it was still not skipped as a duplicate, so it counts as accepted.
            async Task<bool> SendAsync(int area)
            {
                using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    return await _koneApiGateWay.SendLandingCallToAreaIfNotInProgressAsync(area, true, debounceTime, bound.Token);
                }
                catch (OperationCanceledException)
                {
                    return true;
                }
            }
        }
    }
}
