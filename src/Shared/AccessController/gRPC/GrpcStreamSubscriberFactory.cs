namespace SmartFace.Integrations.Fingera.Clients.Grpc
{
    public class GrpcStreamSubscriberFactory
    {
        public GrpcStreamSubscriberFactory()
        {
        }

        public IGrpcStreamSubscriber Create(string host, int port)
        {
            return new GrpcStreamSubscriber(host, port);
        }
    }
}
