using System;
using System.Threading.Tasks;

using Innovatrics.Smartface;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Utils;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessController.Readers
{
    public class GrpcAccessNotificationReader : IAsyncDisposable
    {
        private readonly object _lock = new object();

        public event Action<Notification> OnGrpcAnyNotification;
        public event Func<DateTime, Task> OnGrpcPing;
        public event Func<GrantedNotification, Task> OnGrpcGrantedNotification;
        public event Action<DeniedNotification> OnGrpcDeniedNotification;
        public event Action<BlockedNotification> OnGrpcBlockedNotification;
        public event Action<Exception> OnGrpcError;

        private readonly IGrpcStreamSubscriber _grpcStreamSubscriber;
        private readonly ILogger _log;

        public GrpcAccessNotificationReader(IGrpcStreamSubscriber grpcStreamSubscriber, ILogger log)
        {
            _grpcStreamSubscriber = grpcStreamSubscriber ?? throw new ArgumentNullException(nameof(grpcStreamSubscriber));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void StartReceiving()
        {
            _grpcStreamSubscriber.OnMessageReceived += AccessReceived;
            _grpcStreamSubscriber.OnError += OnError;
            _grpcStreamSubscriber.Subscribe();
        }

        private void AccessReceived(object source, AccessNotification accessNotification)
        {
            // logger.Information("Received Notification {type} with props {@props} processing...", accessNotification.TypeOfAccessNotification, new { FaceId = accessNotification.FaceId, SentAt = accessNotification.SentAt });

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
            _log.Error(e, "Grpc message reading failed");
            OnGrpcError?.Invoke(e);
        }

        private static bool IsGrantedMessage(AccessNotification notification)
        {
            var type = (AccessNotificationType)notification.TypeOfAccessNotification;

            return type.HasFlag(AccessNotificationType.FaceGranted) ||
                   type.HasFlag(AccessNotificationType.OpticalCodeGranted) ||
                   type.HasFlag(AccessNotificationType.PalmGranted);
        }

        private static bool IsDeniedMessage(AccessNotification notification)
        {
            var type = (AccessNotificationType)notification.TypeOfAccessNotification;

            return type.HasFlag(AccessNotificationType.FaceDenied) ||
                   type.HasFlag(AccessNotificationType.OpticalCodeDeniedUnsupported) ||
                   type.HasFlag(AccessNotificationType.PalmDeniedUnsupported);
        }

        private static bool IsBlockedMessage(AccessNotification notification)
        {
            var type = (AccessNotificationType)notification.TypeOfAccessNotification;

            return type.HasFlag(AccessNotificationType.FaceBlocked) ||
                   type.HasFlag(AccessNotificationType.OpticalCodeBlocked) ||
                   type.HasFlag(AccessNotificationType.PalmBlocked);
        }

        private bool IsPingNotification(AccessNotification notification)
        {
            return notification.TypeOfAccessNotification == (uint)AccessNotificationType.Ping;
        }

        public async ValueTask DisposeAsync()
        {
            lock (_lock)
            {
                _grpcStreamSubscriber.OnMessageReceived -= AccessReceived;
                _grpcStreamSubscriber.OnError -= OnError;
            }

            await _grpcStreamSubscriber.DisposeAsync();
        }
    }
}
