using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    public delegate void NotificationReceived(string topic, string json);
    public delegate void ErrorOccured(Exception exception);

    public class ZeroMqNotificationReader : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly CancellationToken _cancellationToken;

        private Task _receiveTask;

        public string EndPoint => $">tcp://{_host}:{_port}";
        public bool Initialized { get; private set; }

        public event NotificationReceived OnNotificationReceived;

        public event ErrorOccured OnError;

        public ZeroMqNotificationReader(string host, int port, CancellationToken cancellationToken)
        {
            _host = host;
            _port = port;
            _cancellationToken = cancellationToken;
        }

        public ZeroMqNotificationReader Init()
        {
            if (Initialized)
                throw new InvalidOperationException($"{nameof(ZeroMqNotificationReader)} already initialized.");

            var completionSource = new TaskCompletionSource<object>();

            _receiveTask = Task.Factory.StartNew(() =>
            {
                var timeout = TimeSpan.FromSeconds(1);

                using (var subSocket = new SubscriberSocket(EndPoint))
                {
                    try
                    {
                        subSocket.Options.ReceiveHighWatermark = 1000;
                        subSocket.SubscribeToAnyTopic();

                        while (!_cancellationToken.IsCancellationRequested)
                        {
                            var zMessage = new NetMQMessage(2);
                            var messageReceived = subSocket.TryReceiveMultipartMessage(timeout, ref zMessage, 2);
                            completionSource.TrySetResult(null);

                            if (!messageReceived)
                            {
                                continue;
                            }

                            var topic = zMessage.Pop().ConvertToString(Encoding.UTF8);
                            var json = zMessage.Pop().ConvertToString(Encoding.UTF8);

                            OnNotificationReceived?.Invoke(topic, json);
                        }
                    }
                    catch (Exception e)
                    {
                        OnError?.Invoke(e);
                    }
                }

            }, TaskCreationOptions.LongRunning);

            _receiveTask.ContinueWith(t =>
            {
                // Propagate exception from initialization if occured
                if (t.Exception != null)
                {
                    completionSource.TrySetException(t.Exception);
                }
            });

            completionSource.Task.GetAwaiter().GetResult();

            Initialized = true;
            return this;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Wait for completion
                _receiveTask?.GetAwaiter().GetResult();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ZeroMqNotificationReader()
        {
            Dispose(false);
        }
    }
}
