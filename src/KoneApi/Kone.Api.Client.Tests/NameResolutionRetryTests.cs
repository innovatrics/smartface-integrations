using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Net.WebSockets;
using Kone.Api.Client.Clients;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Kone.Api.Client.Tests
{
    /// <summary>
    /// KoneBuildingApiClient connects once more when the KONE host name could not be resolved, and only then. These run
    /// the real HTTP and websocket stack. Token requests go to a fake handler. The websocket handshake goes either straight
    /// to a local fake KONE, or through FakeHandshake, whose connect step fails the way a DNS lookup does.
    /// </summary>
    public class NameResolutionRetryTests
    {
        private const string Unreachable = "ws://127.0.0.1:1/stream-v2";
        private static readonly TimeSpan ShortRetryDelay = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);

        [Theory]
        [InlineData(SocketError.HostNotFound)] // the name does not resolve; also what glibc reports when only A fails
        [InlineData(SocketError.TryAgain)] // the DNS servers gave no usable answer
        [InlineData(SocketError.NoData)] // Windows: the name has no address
        public async Task Connects_again_and_succeeds_after_the_token_request_could_not_resolve_the_host(SocketError error)
        {
            await using var kone = new FakeKoneServer();
            var tokens = new FakeTokenHandler(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException(
                HttpRequestError.NameResolutionError, "Name or service not known (dev.kone.com:443)",
                new SocketException((int)error))), FakeTokenHandler.Token);
            var sink = new CollectingSink();

            var pong = await Client(tokens, kone.Endpoint, null, sink, ShortRetryDelay)
                .PingAsync(CancellationToken.None).WaitAsync(Guard);

            Assert.Equal("ping", pong.callType);
            Assert.Equal(2, tokens.Requests);
            var warning = Assert.Single(sink.Events, e => e.Level == LogEventLevel.Warning);
            Assert.StartsWith("Could not resolve the KONE host", warning.MessageTemplate.Text);
            Assert.Contains("dev.kone.com:443", warning.Properties["Error"].ToString());
        }

        [Fact]
        public async Task Connects_again_and_succeeds_after_the_websocket_handshake_could_not_resolve_the_host()
        {
            // 17 of the 30 production failures were here, after the token request had succeeded
            await using var kone = new FakeKoneServer();
            using var handshake = new FakeHandshake(kone, SocketError.HostNotFound);
            var tokens = new FakeTokenHandler(FakeTokenHandler.Token);
            var sink = new CollectingSink();

            var pong = await Client(tokens, FakeHandshake.Endpoint, handshake.Invoker, sink, ShortRetryDelay)
                .PingAsync(CancellationToken.None).WaitAsync(Guard);

            Assert.Equal("ping", pong.callType);
            Assert.Equal(2, handshake.Attempts);
            Assert.Equal(2, tokens.Requests);
            var warning = Assert.Single(sink.Events, e => e.Level == LogEventLevel.Warning);
            Assert.Contains("kone.test:80", warning.Properties["Error"].ToString());
        }

        [Fact]
        public async Task Gives_up_after_one_retry_when_the_websocket_host_still_cannot_be_resolved()
        {
            await using var kone = new FakeKoneServer();
            using var handshake = new FakeHandshake(kone, SocketError.HostNotFound, SocketError.HostNotFound, SocketError.HostNotFound);
            var tokens = new FakeTokenHandler(FakeTokenHandler.Token);
            var sink = new CollectingSink();

            var e = await Assert.ThrowsAsync<WebSocketException>(() =>
                Client(tokens, FakeHandshake.Endpoint, handshake.Invoker, sink, ShortRetryDelay)
                    .PingAsync(CancellationToken.None).WaitAsync(Guard));

            // The chain production logs: WebSocketException > HttpRequestException > SocketException
            var http = Assert.IsType<HttpRequestException>(e.InnerException);
            Assert.Equal(HttpRequestError.NameResolutionError, http.HttpRequestError);
            Assert.Equal(SocketError.HostNotFound, Assert.IsType<SocketException>(http.InnerException).SocketErrorCode);
            Assert.Equal(2, handshake.Attempts);
            Assert.Single(sink.Events, x => x.Level == LogEventLevel.Warning);
        }

        [Fact]
        public async Task Does_not_connect_again_after_a_failure_that_is_not_name_resolution()
        {
            await using var kone = new FakeKoneServer();
            using var handshake = new FakeHandshake(kone, SocketError.ConnectionRefused);
            var tokens = new FakeTokenHandler(FakeTokenHandler.Token);
            var sink = new CollectingSink();

            var e = await Assert.ThrowsAsync<WebSocketException>(() =>
                Client(tokens, FakeHandshake.Endpoint, handshake.Invoker, sink, ShortRetryDelay)
                    .PingAsync(CancellationToken.None).WaitAsync(Guard));

            var http = Assert.IsType<HttpRequestException>(e.InnerException);
            Assert.Equal(SocketError.ConnectionRefused, Assert.IsType<SocketException>(http.InnerException).SocketErrorCode);
            Assert.Equal(1, handshake.Attempts);
            Assert.Equal(1, tokens.Requests);
            Assert.DoesNotContain(sink.Events, x => x.Level >= LogEventLevel.Warning);
        }

        [Fact]
        public async Task Does_not_connect_again_when_the_caller_cancelled_while_the_lookup_failed()
        {
            using var caller = new CancellationTokenSource();
            var tokens = new FakeTokenHandler(ct =>
            {
                caller.Cancel();
                return FakeTokenHandler.NameNotResolved(ct);
            });
            var sink = new CollectingSink();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Client(tokens, Unreachable, null, sink, ShortRetryDelay).PingAsync(caller.Token).WaitAsync(Guard));

            Assert.Equal(1, tokens.Requests);
            Assert.DoesNotContain(sink.Events, x => x.Level >= LogEventLevel.Warning);
        }

        [Fact]
        public async Task Reports_the_DNS_failure_when_the_caller_cancels_while_waiting_to_connect_again()
        {
            using var caller = new CancellationTokenSource();
            var tokens = new FakeTokenHandler(FakeTokenHandler.NameNotResolved);
            // The retry warning is logged just before the 20 s wait; cancelling 100 ms later lands in the middle of it
            var sink = new CollectingSink(e =>
            {
                if (e.Level == LogEventLevel.Warning)
                {
                    caller.CancelAfter(TimeSpan.FromMilliseconds(100));
                }
            });
            var watch = Stopwatch.StartNew();

            var e = await Assert.ThrowsAsync<HttpRequestException>(() =>
                Client(tokens, Unreachable, null, sink, TimeSpan.FromSeconds(20)).PingAsync(caller.Token).WaitAsync(Guard));

            Assert.Equal(HttpRequestError.NameResolutionError, e.HttpRequestError);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
            Assert.Equal(1, tokens.Requests);
            Assert.Single(sink.Events, x => x.Level == LogEventLevel.Warning);
            Assert.Contains(sink.Events, x => x.MessageTemplate.Text.StartsWith("Stopped connecting again"));
        }

        [Fact]
        public async Task A_retry_cut_short_by_the_no_update_window_is_reported_as_the_DNS_failure()
        {
            // The window (2 s) ends while the client waits to connect again (20 s). The call was never placed, so the
            // gateway must fail with the DNS error, not report "no call state ... whether the lift was called is unknown".
            await using var kone = new FakeKoneServer();
            var tokens = new FakeTokenHandler(FakeTokenHandler.NameNotResolved, FakeTokenHandler.Token);
            var sink = new CollectingSink();
            var log = Logger(sink);
            var gateway = new KoneApiGateWay(new KoneBuildingApiClient(log, new KoneAuthApiClient("id", "secret", tokens),
                "building", "group", kone.Endpoint, TimeSpan.FromSeconds(20)), log);
            using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await Assert.ThrowsAsync<HttpRequestException>(() => gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, TimeSpan.FromSeconds(2), caller.Token)
                .WaitAsync(Guard));

            Assert.Equal(0, kone.LandingCalls);
            Assert.Single(sink.Events, e => e.MessageTemplate.Text.StartsWith("Could not resolve the KONE host"));
            Assert.Contains(sink.Events, e => e.MessageTemplate.Text.StartsWith("Stopped connecting again"));
            Assert.DoesNotContain(sink.Events, e => e.MessageTemplate.Text.Contains("no call state"));
        }

        [Fact]
        public async Task A_second_connect_cut_short_by_the_no_update_window_is_reported_as_the_DNS_failure()
        {
            // The retry starts at once, but its token request hangs until the window (2 s) cancels it
            await using var kone = new FakeKoneServer();
            var tokens = new FakeTokenHandler(FakeTokenHandler.NameNotResolved, async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("not reached");
            });
            var sink = new CollectingSink();
            var log = Logger(sink);
            var gateway = new KoneApiGateWay(new KoneBuildingApiClient(log, new KoneAuthApiClient("id", "secret", tokens),
                "building", "group", kone.Endpoint, TimeSpan.Zero), log);
            using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await Assert.ThrowsAsync<HttpRequestException>(() => gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, TimeSpan.FromSeconds(2), caller.Token)
                .WaitAsync(Guard));

            Assert.Equal(2, tokens.Requests);
            Assert.Equal(0, kone.LandingCalls);
            Assert.Single(sink.Events, e => e.MessageTemplate.Text.StartsWith("Could not resolve the KONE host"));
            Assert.Contains(sink.Events, e => e.MessageTemplate.Text.StartsWith("Stopped connecting again"));
            Assert.DoesNotContain(sink.Events, e => e.MessageTemplate.Text.Contains("no call state"));
        }

        [Fact]
        public async Task A_monitor_connect_that_cannot_resolve_the_host_is_retried_without_placing_the_call_again()
        {
            // Token requests: the landing call's, the monitor's (fails), the monitor's retry
            await using var kone = new FakeKoneServer();
            var tokens = new FakeTokenHandler(FakeTokenHandler.Token, FakeTokenHandler.NameNotResolved, FakeTokenHandler.Token);
            var sink = new CollectingSink();
            var log = Logger(sink);
            var gateway = new KoneApiGateWay(new KoneBuildingApiClient(log, new KoneAuthApiClient("id", "secret", tokens),
                "building", "group", kone.Endpoint, ShortRetryDelay), log);
            using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var placed = await gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, TimeSpan.FromSeconds(5), caller.Token)
                .WaitAsync(Guard);

            Assert.True(placed);
            Assert.Equal(3, tokens.Requests);
            Assert.Equal(1, kone.LandingCalls);
            Assert.Equal(1, kone.Monitors);
            Assert.Single(sink.Events, e => e.Level == LogEventLevel.Warning);
            Assert.DoesNotContain(sink.Events, e => e.MessageTemplate.Text.Contains("no call state"));
        }

        [Fact]
        public async Task A_monitor_retry_cut_short_by_the_no_update_window_still_ends_as_no_call_state()
        {
            // KONE accepted the call, so a cancelled monitor retry must not turn into a DNS error ("failed"): the call
            // ends like any other call without call states
            await using var kone = new FakeKoneServer();
            var tokens = new FakeTokenHandler(FakeTokenHandler.Token, FakeTokenHandler.NameNotResolved);
            var sink = new CollectingSink();
            var log = Logger(sink);
            var gateway = new KoneApiGateWay(new KoneBuildingApiClient(log, new KoneAuthApiClient("id", "secret", tokens),
                "building", "group", kone.Endpoint, TimeSpan.FromSeconds(20)), log);
            using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var placed = await gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, TimeSpan.FromSeconds(2), caller.Token)
                .WaitAsync(Guard);

            Assert.True(placed);
            Assert.Equal(2, tokens.Requests);
            Assert.Equal(1, kone.LandingCalls);
            Assert.Equal(0, kone.Monitors);
            Assert.Single(sink.Events, e => e.MessageTemplate.Text.StartsWith("Could not resolve the KONE host"));
            Assert.Contains(sink.Events, e => e.MessageTemplate.Text.StartsWith("KONE accepted the call; stopped connecting its call monitor again"));
            Assert.Contains(sink.Events, e => e.MessageTemplate.Text.Contains("no call state"));
            Assert.DoesNotContain(sink.Events, e => e.MessageTemplate.Text.StartsWith("Stopped connecting again"));
        }

        [Fact]
        public async Task A_call_whose_monitor_cannot_connect_at_all_is_not_reported_as_failed()
        {
            // KONE accepted the call, then both connects of its call monitor fail on their own. The lift was called, so
            // the call ends like any call without call states, not with an exception
            await using var kone = new FakeKoneServer();
            var tokens = new FakeTokenHandler(FakeTokenHandler.Token, FakeTokenHandler.NameNotResolved, FakeTokenHandler.NameNotResolved);
            var sink = new CollectingSink();
            var log = Logger(sink);
            var gateway = new KoneApiGateWay(new KoneBuildingApiClient(log, new KoneAuthApiClient("id", "secret", tokens),
                "building", "group", kone.Endpoint, ShortRetryDelay), log);
            using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            // The default window: the call returns as soon as the monitor gives up, so a long window costs nothing
            var placed = await gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, KoneApiGateWay.DefaultMaxUpdateWaitTime, caller.Token)
                .WaitAsync(Guard);

            Assert.True(placed);
            Assert.Equal(3, tokens.Requests);
            Assert.Equal(1, kone.LandingCalls);
            Assert.Equal(0, kone.Monitors);
            Assert.Single(sink.Events, e => e.MessageTemplate.Text.StartsWith("Could not resolve the KONE host"));
            Assert.Contains(sink.Events, e => e.MessageTemplate.Text.StartsWith("KONE accepted the call (session {SessionId}), but its call monitor failed"));
            Assert.Contains(sink.Events, e => e.MessageTemplate.Text.Contains("no call state"));
        }

        [Fact]
        public async Task A_landing_call_whose_first_lookup_fails_after_4_s_is_placed_once_within_the_default_window()
        {
            // As on the site: a failed lookup of dev.kone.com takes 4 s with docker's resolver. The client waits
            // DefaultNameResolutionRetryDelay and connects again, and the first call state must still arrive within
            // the default no-update window. Otherwise the gateway stops waiting before the call is even placed.
            await using var kone = new FakeKoneServer();
            var tokens = new FakeTokenHandler(
                async ct =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(4), ct);
                    return await FakeTokenHandler.NameNotResolved(ct);
                },
                FakeTokenHandler.Token);
            var sink = new CollectingSink();
            var log = Logger(sink);
            var gateway = new KoneApiGateWay(
                new KoneBuildingApiClient(log, new KoneAuthApiClient("id", "secret", tokens), "building", "group", kone.Endpoint), log);
            using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var started = DateTime.UtcNow;
            var watch = Stopwatch.StartNew();

            var placed = await gateway
                .SendLandingCallToAreaIfNotInProgressAsync(1000, true, KoneApiGateWay.DefaultMaxUpdateWaitTime, caller.Token)
                .WaitAsync(Guard);

            Assert.True(placed);
            Assert.True(watch.Elapsed >= TimeSpan.FromSeconds(4.9), $"took {watch.Elapsed}: no failed lookup and retry delay");
            Assert.Equal(1, kone.LandingCalls);
            Assert.Equal(1, kone.Monitors);
            Assert.DoesNotContain(sink.Events, e => e.MessageTemplate.Text.Contains("no call state"));
            // The call carries the time it was sent, not the time it was started
            var sentAt = DateTime.Parse(kone.LastLandingCallTime!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.True(sentAt - started >= TimeSpan.FromSeconds(4.9), $"payload.time {kone.LastLandingCallTime} is not after the retry");
        }

        [Fact]
        public void The_retry_delay_must_not_be_negative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Client(new FakeTokenHandler(FakeTokenHandler.Token),
                Unreachable, null, new CollectingSink(), TimeSpan.FromMilliseconds(-1)));
        }

        private static KoneBuildingApiClient Client(FakeTokenHandler tokens, string endpoint, HttpMessageInvoker? webSocketInvoker,
            ILogEventSink sink, TimeSpan retryDelay) =>
            new(Logger(sink), new KoneAuthApiClient("id", "secret", tokens), "building", "group", endpoint, retryDelay,
                webSocketInvoker);

        private static ILogger Logger(ILogEventSink sink) =>
            new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        private sealed class CollectingSink(Action<LogEvent>? onEmit = null) : ILogEventSink
        {
            private readonly ConcurrentQueue<LogEvent> _events = new();

            public IEnumerable<LogEvent> Events => _events;

            public void Emit(LogEvent logEvent)
            {
                _events.Enqueue(logEvent);
                onEmit?.Invoke(logEvent);
            }
        }
    }
}
