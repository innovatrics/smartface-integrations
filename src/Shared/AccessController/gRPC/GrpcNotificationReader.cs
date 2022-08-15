using System;
using Innovatrics.Smartface;

using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Utils;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AccessController.Readers
{
    public class GrpcNotificationReader : IAsyncDisposable
    {
        private readonly object _lock = new object();

        public event Action<Notification> OnGrpcAnyNotification;
        public event Func<DateTime, Task> OnGrpcPing;
        public event Func<GrantedNotification, Task> OnGrpcGrantedNotification;
        public event Action<DeniedNotification> OnGrpcDeniedNotification;
        public event Action<BlockedNotification> OnGrpcBlockedNotification;
        public event Action<Exception> OnGrpcError;

        private readonly IGrpcStreamSubscriber grpcStreamSubscriber;

        public GrpcNotificationReader(IGrpcStreamSubscriber grpcStreamSubscriber)
        {
            this.grpcStreamSubscriber = grpcStreamSubscriber ?? throw new ArgumentNullException(nameof(grpcStreamSubscriber));
        }

        public void StartReceiving()
        {
            this.grpcStreamSubscriber.OnMessageReceived += AccessReceived;
            this.grpcStreamSubscriber.OnError += OnError;
            this.grpcStreamSubscriber.Subscribe();
        }

        private void AccessReceived(object source, AccessNotification accessNotification)
        {
            // logger.Information("Received Notification {type} with props {@props} processing...", accessNotification.TypeOfAccessNotification, new { FaceId = accessNotification.FaceId, FaceDetectedAt = accessNotification.FaceDetectedAt });

            OnGrpcAnyNotification?.Invoke(accessNotification.GetNotification());

            if (IsPingNotification(accessNotification))
            {
                var ping = accessNotification.GetNotification();
                OnGrpcPing?.Invoke(ping.GrpcSentAt);
                return;
            }

            if (IsGrantedMessage(accessNotification))
            {
                var granted = accessNotification.GetGrantedNotification();
                OnGrpcGrantedNotification?.Invoke(granted);
                return;
            }

            if (IsDeniedMessage(accessNotification))
            {
                var denied = accessNotification.GetDeniedNotification();
                OnGrpcDeniedNotification?.Invoke(denied);
                return;
            }

            if (IsBlockedMessage(accessNotification))
            {
                var blocked = accessNotification.GetBlockedNotification();
                OnGrpcBlockedNotification?.Invoke(blocked);
                return;
            }
        }

        private void OnError(object source, Exception e)
        {
            OnGrpcError?.Invoke(e);
        }

        private static bool IsGrantedMessage(AccessNotification notification)
        {
            return notification.TypeOfAccessNotification == (uint)AccessNotificationType.Granted;
        }

        private static bool IsDeniedMessage(AccessNotification notification)
        {
            return notification.TypeOfAccessNotification == (uint)AccessNotificationType.Denied;
        }

        private static bool IsBlockedMessage(AccessNotification notification)
        {
            return notification.TypeOfAccessNotification == (uint)AccessNotificationType.Blacklist;
        }

        private bool IsPingNotification(AccessNotification notification)
        {
            return notification.TypeOfAccessNotification == (uint)AccessNotificationType.Ping;
        }

        public async ValueTask DisposeAsync()
        {
            lock (_lock)
            {
                grpcStreamSubscriber.OnMessageReceived -= AccessReceived;
                grpcStreamSubscriber.OnError -= OnError;
            }

            await grpcStreamSubscriber.DisposeAsync();
        }
    }
}
