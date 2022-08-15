using System;
using System.Threading.Tasks;
using Innovatrics.Smartface;

namespace Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc
{
    public interface IGrpcStreamSubscriber : IAsyncDisposable
    {
        event EventHandler<AccessNotification> OnMessageReceived;
        event EventHandler<Exception> OnError;
        void Subscribe();
    }
}
