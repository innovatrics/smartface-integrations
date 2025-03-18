using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Serilog;
using Innovatrics.SmartFace.StoreNotifications.Models;

namespace Innovatrics.SmartFace.StoreNotifications.Services
{
    public class PedestrianObserver : IObserver<GraphQLResponse<PedestrianProcessedResponse>>
    {
        private readonly ILogger _logger;

        public event Func<PedestrianProcessedNotification, Task> OnNotification;

        public PedestrianObserver(ILogger logger)
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

        public void OnNext(GraphQLResponse<PedestrianProcessedResponse> response)
        {
            if (response.Data != null)
            {
                _logger.Information("PedestrianProcessed received for stream {Stream} and PedestrianOrder {PedestrianOrder}",
                    response.Data.PedestrianProcessed?.FrameInformation?.StreamId, response.Data.PedestrianProcessed?.PedestrianInformation?.PedestrianOrder);

                OnNotification?.Invoke(response.Data.PedestrianProcessed);
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