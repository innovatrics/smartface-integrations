using Kone.Api.Client.Clients.Extensions;
using Kone.Api.Client.Exceptions;

namespace Kone.Api.Client.Tests
{
    /// <summary>
    /// Unit tests of PlaceLandingCallUntilServedOrNoUpdateForAsync with a fake IKoneBuildingApi (no KONE connection).
    /// The no-update window may end the wait only when KONE sent no call state at all.
    /// </summary>
    public class PlaceLandingCallUntilServedTests
    {
        private static readonly TimeSpan NoUpdateWindow = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan WellPastTheWindow = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task Keeps_waiting_past_the_no_update_window_once_a_call_state_has_arrived()
        {
            var served = new TaskCompletionSource();
            var api = new FakeKoneBuildingApi(async (callStateReceived, ct) =>
            {
                callStateReceived?.Invoke("call_state registered");
                await served.Task.WaitAsync(ct);
            });

            var call = api.PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, CancellationToken.None);

            await Task.Delay(WellPastTheWindow);
            Assert.False(call.IsCompleted, "the wait ended at the no-update window although a call state had arrived");

            served.SetResult();
            var callStates = await call.WaitAsync(Guard);
            Assert.Single(callStates);
        }

        [Fact]
        public async Task Stops_quietly_when_no_call_state_arrives_within_the_window()
        {
            var api = new FakeKoneBuildingApi((_, ct) => Task.Delay(Timeout.Infinite, ct));

            var callStates = await api
                .PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, CancellationToken.None)
                .WaitAsync(Guard);

            Assert.Empty(callStates);
        }

        [Fact]
        public async Task Throws_when_the_caller_cancels_after_call_states_arrived_but_before_served()
        {
            var api = new FakeKoneBuildingApi(async (callStateReceived, ct) =>
            {
                callStateReceived?.Invoke("call_state assigned");
                await Task.Delay(Timeout.Infinite, ct);
            });
            using var caller = new CancellationTokenSource();

            var call = api.PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, caller.Token);

            await Task.Delay(WellPastTheWindow);
            Assert.False(call.IsCompleted, "the wait ended before the caller cancelled");

            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(Guard));
        }

        [Fact]
        public async Task Keeps_waiting_when_the_first_call_state_arrives_on_another_thread_within_the_window()
        {
            // A wide window, so a slow thread pool cannot deliver the 30 ms state after the window has fired
            var window = TimeSpan.FromSeconds(1);
            var served = new TaskCompletionSource();
            var api = new FakeKoneBuildingApi(async (callStateReceived, ct) =>
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(30);
                    callStateReceived?.Invoke("call_state registered");
                });
                await served.Task.WaitAsync(ct);
            });

            var call = api.PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, window, CancellationToken.None);

            await Task.Delay(window + TimeSpan.FromMilliseconds(500));
            Assert.False(call.IsCompleted, "the wait ended although a call state arrived within the window");

            served.SetResult();
            Assert.Single(await call.WaitAsync(Guard));
        }

        [Fact]
        public async Task Throws_KoneCallException_at_once_when_KONE_cancels_the_call()
        {
            var api = new FakeKoneBuildingApi(async (callStateReceived, ct) =>
            {
                callStateReceived?.Invoke(CallState("registered"));
                callStateReceived?.Invoke(CallState("cancelled"));
                await Task.Delay(Timeout.Infinite, ct);
            });

            // No caller timeout: without the cancelled handling this would wait until the guard fires
            var call = api.PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, CancellationToken.None);

            var e = await Assert.ThrowsAsync<KoneCallException>(() => call.WaitAsync(Guard));
            Assert.Contains("cancelled", e.JsonMessage);
        }

        [Fact]
        public async Task Throws_KoneCallException_when_KONE_cancels_and_the_api_returns_normally()
        {
            var api = new FakeKoneBuildingApi((callStateReceived, _) =>
            {
                callStateReceived?.Invoke(CallState("cancelled"));
                return Task.CompletedTask;
            });

            await Assert.ThrowsAsync<KoneCallException>(() => api
                .PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, CancellationToken.None)
                .WaitAsync(Guard));
        }

        [Fact]
        public async Task Throws_KoneCallException_when_KONE_cancels_from_another_thread_while_waiting()
        {
            var api = new FakeKoneBuildingApi(async (callStateReceived, ct) =>
            {
                callStateReceived?.Invoke(CallState("registered"));
                _ = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    callStateReceived?.Invoke(CallState("cancelled"));
                });
                await Task.Delay(Timeout.Infinite, ct);
            });

            await Assert.ThrowsAsync<KoneCallException>(() => api
                .PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, CancellationToken.None)
                .WaitAsync(Guard));
        }

        [Fact]
        public async Task A_cancelled_state_after_served_does_not_fail_the_call()
        {
            var api = new FakeKoneBuildingApi((callStateReceived, _) =>
            {
                callStateReceived?.Invoke(CallState("registered"));
                callStateReceived?.Invoke(CallState("served"));
                callStateReceived?.Invoke(CallState("cancelled"));
                return Task.CompletedTask;
            });

            var callStates = await api
                .PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, CancellationToken.None)
                .WaitAsync(Guard);

            Assert.Equal(3, callStates.Length);
        }

        [Fact]
        public async Task Messages_that_are_not_call_state_objects_are_kept_but_do_not_end_the_call()
        {
            var api = new FakeKoneBuildingApi((callStateReceived, _) =>
            {
                callStateReceived?.Invoke("42");
                callStateReceived?.Invoke("[]");
                callStateReceived?.Invoke("{\"subtopic\":5}");
                callStateReceived?.Invoke("{\"subtopic\":\"lift_1/position\"}");
                callStateReceived?.Invoke(CallState("served"));
                return Task.CompletedTask;
            });

            var callStates = await api
                .PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, CancellationToken.None)
                .WaitAsync(Guard);

            Assert.Equal(5, callStates.Length);
        }

        [Fact]
        public async Task A_cancelled_callback_after_the_call_returned_does_not_throw_into_the_caller()
        {
            Action<string>? callStateReceived = null;
            var api = new FakeKoneBuildingApi((callback, _) =>
            {
                callStateReceived = callback;
                callback?.Invoke(CallState("registered"));
                return Task.CompletedTask;
            });

            await api.PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, CancellationToken.None)
                .WaitAsync(Guard);

            // An implementation that delivers a state after the method returned must not get an exception back
            Assert.Null(Record.Exception(() => callStateReceived!(CallState("cancelled"))));
        }

        [Fact]
        public async Task Returns_the_call_states_when_served_within_the_window()
        {
            var api = new FakeKoneBuildingApi((callStateReceived, _) =>
            {
                callStateReceived?.Invoke("call_state registered");
                callStateReceived?.Invoke("call_state served");
                return Task.CompletedTask;
            });

            var callStates = await api
                .PlaceLandingCallUntilServedOrNoUpdateForAsync(1000, true, NoUpdateWindow, CancellationToken.None)
                .WaitAsync(Guard);

            Assert.Equal(2, callStates.Length);
        }

        private static string CallState(string state) =>
            "{\"callType\":\"monitor\",\"subtopic\":\"call_state/42/" + state + "\",\"data\":{}}";
    }
}
