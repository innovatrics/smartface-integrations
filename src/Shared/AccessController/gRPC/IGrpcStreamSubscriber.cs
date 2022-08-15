using System;
using System.Threading.Tasks;
using Innovatrics.Smartface;

namespace SmartFace.Integrations.Fingera.Clients.Grpc
{
    public interface IGrpcStreamSubscriber : IAsyncDisposable
    {
        event EventHandler<AccessNotification> OnMessageReceived;
        event EventHandler<Exception> OnError;
        void Subscribe();
    }
}
