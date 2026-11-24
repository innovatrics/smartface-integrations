using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AccessControlConnector.Connectors.Kone;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Kone.Api.Client;
using Kone.Api.Client.Exceptions;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Tests.Connectors
{
    /// <summary>
    /// How KoneConnector reports the outcome of a landing call. ERROR is only for calls that did not reach KONE
    /// (DNS, websocket) or that KONE rejected or cancelled. A call KONE accepted but did not report served in time is
    /// information, because a slow lift is normal.
    /// </summary>
    public class KoneConnectorTests
    {
        private const string Failed = "Landing call with Id {LandingCallId} to area {DestinationArea} failed";
        private const string Skipped = "Landing call with Id {LandingCallId} to area {DestinationArea} skipped: a call to this area is already in progress";
        private const string Finished = "Landing call with Id {LandingCallId} to area {DestinationArea} finished";
        private const string NotServedInTime = "Landing call with Id {LandingCallId} to area {DestinationArea} was accepted by KONE (call states received) but not reported served within {LandingCallTimeout}; stopped waiting";

        private static readonly TimeSpan LandingCallTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan NoUpdateWindow = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task Accepted_but_not_served_in_time_is_information_not_an_error()
        {
            var api = Api((callStateReceived, ct) =>
            {
                callStateReceived?.Invoke("call_state assigned");
                return Task.Delay(Timeout.Infinite, ct);
            });
            var sink = new CollectingSink();

            await Connector(api, sink).OpenAsync(new StreamConfig { DestinationArea = 1000, IsDirectionUp = true });
            var outcome = await sink.WaitForOutcomeAsync(Guard);

            Assert.Equal(NotServedInTime, outcome.MessageTemplate.Text);
            Assert.Equal(LogEventLevel.Information, outcome.Level);
            Assert.DoesNotContain(sink.Events, e => e.Level >= LogEventLevel.Warning);
        }

        [Fact]
        public async Task A_call_that_cannot_reach_KONE_is_still_an_error()
        {
            var api = Api((_, _) => Task.FromException(new HttpRequestException("Name or service not known (dev.kone.com:443)")));
            var sink = new CollectingSink();

            await Connector(api, sink).OpenAsync(new StreamConfig { DestinationArea = 1000, IsDirectionUp = true });
            var outcome = await sink.WaitForOutcomeAsync(Guard);

            Assert.Equal(Failed, outcome.MessageTemplate.Text);
            Assert.Equal(LogEventLevel.Error, outcome.Level);
            Assert.IsType<HttpRequestException>(outcome.Exception);
        }

        [Fact]
        public async Task A_call_KONE_cancels_is_the_only_error()
        {
            var api = Api((callStateReceived, ct) =>
            {
                callStateReceived?.Invoke("{\"callType\":\"monitor\",\"subtopic\":\"call_state/42/registered\"}");
                callStateReceived?.Invoke("{\"callType\":\"monitor\",\"subtopic\":\"call_state/42/cancelled\"}");
                return Task.Delay(Timeout.Infinite, ct);
            });
            var sink = new CollectingSink();

            await Connector(api, sink).OpenAsync(new StreamConfig { DestinationArea = 1000, IsDirectionUp = true });
            var outcome = await sink.WaitForOutcomeAsync(Guard);

            Assert.Equal(Failed, outcome.MessageTemplate.Text);
            Assert.IsType<KoneCallException>(outcome.Exception);
            Assert.Single(sink.Events, e => e.Level >= LogEventLevel.Warning);
        }

        [Fact]
        public async Task A_call_served_in_time_is_information()
        {
            var api = Api((callStateReceived, _) =>
            {
                callStateReceived?.Invoke("call_state registered");
                callStateReceived?.Invoke("call_state served");
                return Task.CompletedTask;
            });
            var sink = new CollectingSink();

            await Connector(api, sink).OpenAsync(new StreamConfig { DestinationArea = 1000, IsDirectionUp = true });
            var outcome = await sink.WaitForOutcomeAsync(Guard);

            Assert.Equal(Finished, outcome.MessageTemplate.Text);
            Assert.Equal(LogEventLevel.Information, outcome.Level);
            Assert.DoesNotContain(sink.Events, e => e.Level >= LogEventLevel.Warning);
        }

        [Fact]
        public async Task A_call_with_no_call_state_finishes_with_the_gateway_warning_beside_it()
        {
            var api = Api((_, ct) => Task.Delay(Timeout.Infinite, ct));
            var sink = new CollectingSink();

            await Connector(api, sink).OpenAsync(new StreamConfig { DestinationArea = 1000, IsDirectionUp = true });
            var outcome = await sink.WaitForOutcomeAsync(Guard);

            Assert.Equal(Finished, outcome.MessageTemplate.Text);
            Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Warning &&
                                              e.MessageTemplate.Text.Contains("no call state from KONE"));
            Assert.DoesNotContain(sink.Events, e => e.Level >= LogEventLevel.Error);
        }

        [Fact]
        public async Task A_second_call_to_an_area_in_progress_is_logged_as_skipped()
        {
            var api = Api((callStateReceived, ct) =>
            {
                callStateReceived?.Invoke("call_state assigned");
                return Task.Delay(Timeout.Infinite, ct);
            });
            var sink = new CollectingSink();
            var connector = Connector(api, sink);

            await connector.OpenAsync(new StreamConfig { DestinationArea = 1000, IsDirectionUp = true });
            await connector.OpenAsync(new StreamConfig { DestinationArea = 1000, IsDirectionUp = true });

            var skipped = await sink.WaitForAsync(e => e.MessageTemplate.Text == Skipped, Guard);
            Assert.Equal(LogEventLevel.Information, skipped.Level);
        }

        [Fact]
        public async Task The_default_state_window_outlasts_a_DNS_retry_while_connecting()
        {
            // A failed DNS lookup of the KONE host (4 s with docker's resolver), the retry delay (1 s) and connecting again put
            // the first call state about 6 s after sending. The default window must still be waiting then.
            var api = Api(async (callStateReceived, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(6), ct);
                callStateReceived?.Invoke("{\"callType\":\"monitor\",\"subtopic\":\"call_state/42/served\"}");
            });
            var sink = new CollectingSink();
            var log = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

            await new KoneConnector(new KoneApiGateWay(api, log), log)
                .OpenAsync(new StreamConfig { DestinationArea = 1000, IsDirectionUp = true });
            var outcome = await sink.WaitForOutcomeAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(Finished, outcome.MessageTemplate.Text);
            Assert.DoesNotContain(sink.Events, e => e.Level >= LogEventLevel.Warning);
        }

        [Theory]
        [InlineData(5000, 5000)]
        [InlineData(5000, 6000)]
        [InlineData(0, 100)]
        [InlineData(5000, 0)]
        public void Timeouts_must_be_positive_and_the_state_window_shorter_than_the_landing_call_timeout(
            int landingCallTimeoutMs, int maxStateUpdateWaitTimeMs)
        {
            var log = new LoggerConfiguration().CreateLogger();
            var gateway = new KoneApiGateWay(Substitute.For<IKoneBuildingApi>(), log);

            Assert.ThrowsAny<ArgumentException>(() => new KoneConnector(gateway, log,
                TimeSpan.FromMilliseconds(landingCallTimeoutMs), TimeSpan.FromMilliseconds(maxStateUpdateWaitTimeMs)));
        }

        private static IKoneBuildingApi Api(Func<Action<string>, CancellationToken, Task> placeLandingCall)
        {
            var api = Substitute.For<IKoneBuildingApi>();
            api.PlaceLandingCallWithPositionUpdatesAsync(Arg.Any<int>(), Arg.Any<Action<string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(ci => placeLandingCall(ci.ArgAt<Action<string>>(1), ci.ArgAt<CancellationToken>(3)));
            return api;
        }

        private static KoneConnector Connector(IKoneBuildingApi api, ILogEventSink sink)
        {
            var log = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            return new KoneConnector(new KoneApiGateWay(api, log), log, LandingCallTimeout, NoUpdateWindow);
        }

        private sealed class CollectingSink : ILogEventSink
        {
            private readonly ConcurrentQueue<LogEvent> _events = new();

            public LogEvent[] Events => _events.ToArray();

            public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

            /// <summary>Waits for KoneConnector's outcome record of the landing call ("Landing call with Id ...").</summary>
            public Task<LogEvent> WaitForOutcomeAsync(TimeSpan timeout) =>
                WaitForAsync(e => e.MessageTemplate.Text.StartsWith("Landing call with Id"), timeout);

            public async Task<LogEvent> WaitForAsync(Func<LogEvent, bool> match, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    var found = Events.FirstOrDefault(match);
                    if (found != null)
                    {
                        return found;
                    }

                    await Task.Delay(20);
                }

                throw new TimeoutException($"KoneConnector logged no matching record within {timeout}; logged: " +
                                           string.Join(" | ", Events.Select(e => e.MessageTemplate.Text)));
            }
        }
    }
}
