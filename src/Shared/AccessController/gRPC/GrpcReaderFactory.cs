using System;
using Innovatrics.SmartFace.Integrations.AccessController.Readers;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc
{
    public class GrpcReaderFactory
    {
        private readonly GrpcStreamSubscriberFactory _grpcStreamSubscriberFactory;
        private readonly ILogger _log;

        public GrpcReaderFactory(GrpcStreamSubscriberFactory grpcStreamSubscriberFactory, ILogger log)
        {
            _grpcStreamSubscriberFactory = grpcStreamSubscriberFactory ?? throw new ArgumentNullException(nameof(grpcStreamSubscriberFactory));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public GrpcAccessNotificationReader Create(string host, int port)
        {
            var grpcStreamSubscriber = _grpcStreamSubscriberFactory.Create(host, port);
            return new GrpcAccessNotificationReader(grpcStreamSubscriber, _log.ForContext<GrpcAccessNotificationReader>());
        }
    }
}
