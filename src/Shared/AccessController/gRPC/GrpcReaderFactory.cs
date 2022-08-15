using System;
using SmartFace.Integrations.Fingera.Readers;

namespace SmartFace.Integrations.Fingera.Clients.Grpc
{
    public class GrpcReaderFactory
    {
        private GrpcStreamSubscriberFactory _grpcStreamSubscriberFactory;

        public GrpcReaderFactory(GrpcStreamSubscriberFactory grpcStreamSubscriberFactory)
        {
            _grpcStreamSubscriberFactory = grpcStreamSubscriberFactory ?? throw new ArgumentNullException(nameof(grpcStreamSubscriberFactory));
        }

        public GrpcNotificationReader Create(string host, int port)
        {
            var _grpcStreamSubscriber = _grpcStreamSubscriberFactory.Create(host, port);
            return new GrpcNotificationReader(_grpcStreamSubscriber);
        }
    }
}
