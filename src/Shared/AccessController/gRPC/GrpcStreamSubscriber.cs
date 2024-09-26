using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Innovatrics.Smartface;
using Polly;
using GrpcCore = Grpc.Core;

namespace Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc
{
    public class GrpcStreamSubscriber : IGrpcStreamSubscriber
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly TimeSpan GRPC_TIMEOUT = TimeSpan.FromSeconds(10);

        private Task subscribeTask;
        private readonly ChannelBase grpcChannel;
        private readonly AccessNotificationService.AccessNotificationServiceClient grpcClient;

        public event EventHandler<AccessNotification> OnMessageReceived;
        public event EventHandler<Exception> OnError;

        public GrpcStreamSubscriber(string host, int port)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            grpcChannel = GrpcChannel.ForAddress($"http://{host}:{port}", new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.Insecure,
            });

            grpcClient = new AccessNotificationService.AccessNotificationServiceClient(grpcChannel);
        }

        public void Subscribe()
        {
            this.subscribeTask = startListening();
        }

        private Task startListening()
        {
            return Task.Run(async () =>
                {
                    await backgroundReadingAsync(cancellationTokenSource.Token);
                }                
            );
        }

        public async ValueTask DisposeAsync()
        {
            // Signal cancellation
            cancellationTokenSource.Cancel();
            await subscribeTask;
            await grpcChannel.ShutdownAsync();
            cancellationTokenSource.Dispose();             
        }

        private async Task backgroundReadingAsync(CancellationToken cancellationToken = default)
        {
            var stream = this.GetStream(cancellationToken);
            var cts = new CancellationTokenSource(GRPC_TIMEOUT);

            var policy = Policy
                .Handle<Exception>()
                .WaitAndRetryForeverAsync(
                    attempt => TimeSpan.FromSeconds(15),
                    (exception, timespan) =>
                    {
                        // logger.Error(exception, "gRPC stream error. Reconnecting...");
                        OnError?.Invoke(this, exception);

                        // we haven't received data for more than allowed threshold
                        // reconnecting to the service again
                        stream = this.GetStream(cancellationToken);

                        // here we are recreating cancellation token
                        // and need to take into consideration that parameter `timespan`
                        // is wait duration for the next execution.

                        // meaning that we have to set cancellation token expiration
                        // to `waitDuration + timeout`
                        // otherwise token will be already cancelled when retry policy will execute
                        cts = new CancellationTokenSource(timespan + GRPC_TIMEOUT);
                    });

            await policy.ExecuteAndCaptureAsync(async (CancellationToken cancellationToken) =>
            {
                await foreach (var message in stream.ReadAllAsync(cts.Token))
                {
                    OnMessageReceived?.Invoke(this, message);
                    cts.CancelAfter(GRPC_TIMEOUT);
                }
            }, cancellationToken);
        }

        private AsyncServerStreamingCall<Innovatrics.Smartface.AccessNotification> getStreamingCall(CancellationToken cancellationToken)
        {
            return grpcClient.GetAccessNotifications(new AccessNotificationRequest
            {
                SendImageData = true,
                TypeOfAccessNotification = (uint)AccessNotificationType.FaceGranted
                                                                                   | (uint)AccessNotificationType.FaceGranted
                                                                                   | (uint)AccessNotificationType.FaceBlocked
                                                                                   | (uint)AccessNotificationType.Ping
            }, cancellationToken: cancellationToken);
        }

        private IAsyncStreamReader<Innovatrics.Smartface.AccessNotification> GetStream(CancellationToken cancellationToken = default)
        {
            var call = this.getStreamingCall(cancellationToken);
            return call.ResponseStream;
        }
    }
}
