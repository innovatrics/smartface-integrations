namespace Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc
{
    public class GrpcStreamSubscriberFactory
    {
        public IGrpcStreamSubscriber Create(string host, int port)
        {
            return new GrpcStreamSubscriber(host, port);
        }
    }
}
