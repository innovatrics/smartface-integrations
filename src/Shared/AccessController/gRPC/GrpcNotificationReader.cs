﻿using System;
using System.Threading.Tasks;

using Innovatrics.Smartface;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Utils;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AccessController.Readers
{
    public class GrpcNotificationReader : IAsyncDisposable
    {
        private readonly object _lock = new object();

        public event Action<Notification> OnGrpcAnyNotification;
        public event Func<DateTime, Task> OnGrpcPing;
        public event Func<FaceGrantedNotification, Task> OnGrpcFaceGrantedNotification;
        public event Action<FaceDeniedNotification> OnGrpcFaceDeniedNotification;
        public event Action<FaceBlockedNotification> OnGrpcFaceBlockedNotification;
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
                OnGrpcFaceGrantedNotification?.Invoke(granted);
                return;
            }

            if (IsDeniedMessage(accessNotification))
            {
                var denied = accessNotification.GetDeniedNotification();
                OnGrpcFaceDeniedNotification?.Invoke(denied);
                return;
            }

            if (IsBlockedMessage(accessNotification))
            {
                var blocked = accessNotification.GetBlockedNotification();
                OnGrpcFaceBlockedNotification?.Invoke(blocked);
                return;
            }
        }

        private void OnError(object source, Exception e)
        {
            OnGrpcError?.Invoke(e);
        }

        private static bool IsGrantedMessage(AccessNotification notification)
        {
            return notification.TypeOfAccessNotification == (uint)AccessNotificationType.FaceGranted;
        }

        private static bool IsDeniedMessage(AccessNotification notification)
        {
            return notification.TypeOfAccessNotification == (uint)AccessNotificationType.FaceGranted;
        }

        private static bool IsBlockedMessage(AccessNotification notification)
        {
            return notification.TypeOfAccessNotification == (uint)AccessNotificationType.FaceBlocked;
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
