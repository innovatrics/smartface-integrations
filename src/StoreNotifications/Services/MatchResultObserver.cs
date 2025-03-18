using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Serilog;
using Innovatrics.SmartFace.StoreNotifications.Models;

namespace Innovatrics.SmartFace.StoreNotifications.Services
{
    public class MatchResultObserver : IObserver<GraphQLResponse<MatchResultProcessedResponse>>
    {
        private readonly ILogger _logger;

        public event Func<MatchResultProcessedNotification, Task> OnNotification;

        public MatchResultObserver(ILogger logger)
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

        public void OnNext(GraphQLResponse<MatchResultProcessedResponse> response)
        {
            if (response.Data != null)
            {
                _logger.Information("MatchResultProcessed received for stream {Stream} and FaceOrder {FaceOrder}",
                    response.Data.MatchResultProcessed?.StreamId, response.Data.MatchResultProcessed?.FaceOrder);

                OnNotification?.Invoke(response.Data.MatchResultProcessed);
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
    }
}