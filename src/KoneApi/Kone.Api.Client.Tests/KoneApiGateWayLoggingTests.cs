using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Kone.Api.Client.Tests
{
    /// <summary>
    /// A landing call that got no call state from KONE within the window is reported as accepted by the gateway,
    /// so it must at least leave a warning in the log.
    /// </summary>
    public class KoneApiGateWayLoggingTests
    {
        private static readonly TimeSpan NoUpdateWindow = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task Warns_when_KONE_sends_no_call_state_within_the_window()
        {
            var sink = new CollectingSink();
            var gateway = new KoneApiGateWay(
                new FakeKoneBuildingApi((_, ct) => Task.Delay(Timeout.Infinite, ct)), Logger(sink));

            var accepted = await gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, NoUpdateWindow)
                .WaitAsync(Guard);

            Assert.True(accepted);
            Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Warning &&
                                              e.MessageTemplate.Text.Contains("no call state from KONE"));
        }

        [Fact]
        public async Task Does_not_warn_when_call_states_arrive()
        {
            var sink = new CollectingSink();
            var gateway = new KoneApiGateWay(new FakeKoneBuildingApi((callStateReceived, _) =>
            {
                callStateReceived?.Invoke("call_state served");
                return Task.CompletedTask;
            }), Logger(sink));

            var accepted = await gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, NoUpdateWindow)
                .WaitAsync(Guard);

            Assert.True(accepted);
            Assert.DoesNotContain(sink.Events, e => e.Level >= LogEventLevel.Warning);
        }

        [Fact]
        public async Task Without_a_cancellable_token_the_cap_ends_a_call_that_is_never_served_and_releases_the_area()
        {
            var sink = new CollectingSink();
            var gateway = new KoneApiGateWay(new FakeKoneBuildingApi(
                    async (callStateReceived, ct) =>
                    {
                        callStateReceived?.Invoke("call_state assigned");
                        await Task.Delay(Timeout.Infinite, ct);
                    },
                    (callStateReceived, _) =>
                    {
                        callStateReceived?.Invoke("call_state served");
                        return Task.CompletedTask;
                    }),
                Logger(sink), maxCallDuration: TimeSpan.FromMilliseconds(300));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, NoUpdateWindow)
                .WaitAsync(Guard));

            var next = await gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, NoUpdateWindow)
                .WaitAsync(Guard);
            Assert.True(next, "the area was not released after the capped call ended");
        }

        [Fact]
        public void The_cap_must_be_positive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new KoneApiGateWay(new FakeKoneBuildingApi((_, _) => Task.CompletedTask), Logger(new CollectingSink()), TimeSpan.Zero));
        }

        [Fact]
        public async Task Without_a_cancellable_token_the_window_must_be_shorter_than_the_cap()
        {
            var gateway = new KoneApiGateWay(new FakeKoneBuildingApi((_, _) => Task.CompletedTask),
                Logger(new CollectingSink()), maxCallDuration: TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                gateway.SendLandingCallToAreaIfNotInProgressAsync(1000, true, TimeSpan.FromMilliseconds(100)));
        }

        private static ILogger Logger(ILogEventSink sink) =>
            new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        private sealed class CollectingSink : ILogEventSink
        {
            private readonly ConcurrentQueue<LogEvent> _events = new();
            public IEnumerable<LogEvent> Events => _events;
            public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
        }
    }
}
