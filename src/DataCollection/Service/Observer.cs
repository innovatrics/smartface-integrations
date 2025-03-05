using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Serilog;
using Innovatrics.SmartFace.DataCollection.Models;

namespace Innovatrics.SmartFace.DataCollection.Services
{
    public class Observer : IObserver<GraphQLResponse<MatchResultResponse>>
    {
        private readonly ILogger _logger;

        public event Func<Notification, Task> OnNotification;

        public Observer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(GraphQLResponse<MatchResultResponse> response)
        {
            if (response.Data != null)
            {
                _logger.Information("MatchResult received for stream {Stream} and WatchlistMember {WatchlistMember} ({WatchlistMemberDisplayName})",
                    response.Data.MatchResult?.StreamId, response.Data.MatchResult?.WatchlistMemberId, (response.Data.MatchResult?.WatchlistMemberDisplayName ?? response.Data.MatchResult?.WatchlistMemberFullName));

                var notification = ConvertToNotification(response.Data);

                OnNotification?.Invoke(notification);
            }
            else if (response.Errors != null && response.Errors.Length > 0)
            {
                _logger.Information("{errors} errors from GraphQL received", response.Errors.Length);

                foreach (var e in response.Errors)
                {
                    _logger.Error("{error}", e.Message);
                }
            }
        }

        public static Notification ConvertToNotification(MatchResultResponse response)
        {
            var notification = new Notification
            {
                StreamId = response.MatchResult?.StreamId,
                CropImage = response.MatchResult?.CropImage,
                WatchlistMemberId = response.MatchResult?.WatchlistMemberId,
                WatchlistMemberDisplayName = response.MatchResult?.WatchlistMemberDisplayName,
                WatchlistMemberFullName = response.MatchResult?.WatchlistMemberFullName,
                Score = response.MatchResult?.Score,

                WatchlistId = response.MatchResult?.WatchlistId,

                ReceivedAt = DateTime.UtcNow
            };

            return notification;
        }
    }
}