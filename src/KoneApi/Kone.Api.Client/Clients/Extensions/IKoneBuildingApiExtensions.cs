using System.Collections.Concurrent;

namespace Kone.Api.Client.Clients.Extensions
{
    // ReSharper disable once InconsistentNaming
    public static class IKoneBuildingApiExtensions
    {
        /// <summary>
        /// Wait for landing call to be served fully or set max time that you are willing to wait if there are no position calls made.
        /// </summary>
        /// <returns>Position updates messages</returns>
        public static async Task<string[]> PlaceLandingCallUntilServedOrNoUpdateForAsync(this IKoneBuildingApi koneBuildingApi,
            int destinationAreaId, bool isDirectionUp, TimeSpan maxUpdateWaitTime,
            CancellationToken cancellationToken)
        {
            var positionUpdates = new ConcurrentBag<string>();

            var positionUpdateReceivedTcs = new CancellationTokenSource(maxUpdateWaitTime);

            var joinedTcs = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                positionUpdateReceivedTcs.Token);

            positionUpdateReceivedTcs.Token.Register(() =>
            {
                // Stop waiting if after 5 seconds no position updates were received
                if (positionUpdates.IsEmpty)
                {
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
                    },
                    cancellationToken: joinedTcs.Token);
            }
            catch (OperationCanceledException) when (positionUpdates.IsEmpty)
            {

            }

            return positionUpdates.ToArray();
        }
    }
}
