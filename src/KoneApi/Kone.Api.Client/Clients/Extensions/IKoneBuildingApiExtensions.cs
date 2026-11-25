using System.Collections.Concurrent;
using System.Text.Json;
using Kone.Api.Client.Exceptions;

namespace Kone.Api.Client.Clients.Extensions
{
    // ReSharper disable once InconsistentNaming
    public static class IKoneBuildingApiExtensions
    {
        /// <summary>
        /// Places a landing call and waits until KONE reports it served. If KONE sends no call state within
        /// <paramref name="maxUpdateWaitTime"/>, stops waiting and returns no states. Once call states have arrived,
        /// only <paramref name="cancellationToken"/> bounds the wait, so pass a token with a timeout.
        /// </summary>
        /// <returns>The call state messages received; empty if none arrived within maxUpdateWaitTime.</returns>
        /// <exception cref="KoneCallException">KONE reported the call as cancelled.</exception>
        /// <exception cref="HttpRequestException">KONE could not be reached (also a WebSocketException). This includes a DNS
        /// failure whose retry was cut short by the no-update window: the call was never sent, so no states are returned
        /// for it.</exception>
        /// <exception cref="OperationCanceledException">The caller's token was cancelled after call states had
        /// arrived and before the call was served.</exception>
        public static async Task<string[]> PlaceLandingCallUntilServedOrNoUpdateForAsync(this IKoneBuildingApi koneBuildingApi,
            int destinationAreaId, bool isDirectionUp, TimeSpan maxUpdateWaitTime,
            CancellationToken cancellationToken)
        {
            var positionUpdates = new ConcurrentBag<string>();
            var stoppedForNoUpdate = 0;
            var servedSeen = 0;
            string? cancelledByKone = null;

            // Link only the caller's token. The no-update timer must not be linked: a linked source is cancelled as
            // soon as any of its tokens is, so it would end the wait after maxUpdateWaitTime even when call states
            // have already arrived. The timer ends the wait through the callback below instead.
            // Keep the order of these three declarations: the registration is disposed first, and disposing it waits
            // for a running callback, so joinedTcs is never cancelled after it has been disposed.
            using var joinedTcs = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var positionUpdateReceivedTcs = new CancellationTokenSource(maxUpdateWaitTime);
            using var noUpdateRegistration = positionUpdateReceivedTcs.Token.Register(() =>
            {
                // Stop waiting if no call state was received within maxUpdateWaitTime
                if (positionUpdates.IsEmpty)
                {
                    Interlocked.Exchange(ref stoppedForNoUpdate, 1);
                    joinedTcs.Cancel();
                }
            });

            try
            {
                await koneBuildingApi.PlaceLandingCallWithPositionUpdatesAsync(
                    destinationAreaId: destinationAreaId,
                    isDirectionUp: isDirectionUp,
                    positionUpdated: m =>
                    {
                        positionUpdates.Add(m);

                        switch (CallStateOf(m))
                        {
                            case "served":
                                Interlocked.Exchange(ref servedSeen, 1);
                                break;

                            // A call KONE cancels (for example when the lift is out of service) is never served.
                            // A cancelled state after served does not undo the served call.
                            case "cancelled" when Volatile.Read(ref servedSeen) == 0 &&
                                                  Interlocked.CompareExchange(ref cancelledByKone, m, null) == null:
                                try
                                {
                                    joinedTcs.Cancel();
                                }
                                catch (ObjectDisposedException)
                                {
                                    // A callback after this method has returned: there is nothing left to cancel
                                }

                                break;
                        }
                    },
                    cancellationToken: joinedTcs.Token);
            }
            catch (OperationCanceledException) when (Volatile.Read(ref stoppedForNoUpdate) == 1 ||
                                                     Volatile.Read(ref cancelledByKone) != null)
            {
                // Ended by this method: no call state in time, or KONE cancelled the call (handled below)
            }

            var cancelledMessage = Volatile.Read(ref cancelledByKone);
            if (cancelledMessage != null)
            {
                throw new KoneCallException("Landing call was cancelled by KONE", cancelledMessage);
            }

            return positionUpdates.ToArray();
        }

        // The state of a call_state message ("registered", ..., "served", "cancelled"), taken from the last segment of
        // its subtopic "call_state/{sessionId}/{state}", or null. It runs on the websocket reading loop, so it never
        // throws: a message that does not parse has no state.
        private static string? CallStateOf(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("subtopic", out var subtopic) &&
                    subtopic.ValueKind == JsonValueKind.String &&
                    subtopic.GetString() is { } value &&
                    value.StartsWith("call_state/", StringComparison.Ordinal))
                {
                    return value[(value.LastIndexOf('/') + 1)..];
                }
            }
            catch (Exception)
            {
                // not a call state
            }

            return null;
        }
    }
}
