using Innovatrics.Smartface;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AccessController.Utils
{
    public static class AccessNotificationExtensions
    {
        public static Notification GetNotification(this AccessNotification accessNotification)
        {
            return new Notification
            {
                StreamId = accessNotification.StreamId,
                TrackletId = accessNotification.TrackletId,
                FaceId = accessNotification.FaceId,
                GrpcSentAt = accessNotification.SentAt.ToDateTime(),
                FaceDetectedAt = accessNotification.FaceDetectedAt?.ToDateTime()
            };
        }

        public static GrantedNotification GetGrantedNotification(this AccessNotification accessNotification)
        {
            var accessNotificationGranted = accessNotification.AccessNotificationGranted;
            var grantedNotification = new GrantedNotification
            {
                StreamId = accessNotification.StreamId,
                TrackletId = accessNotification.TrackletId,
                FaceId = accessNotification.FaceId,
                WatchlistMemberId = accessNotificationGranted.WatchlistMemberId,
                WatchlistMemberFullName = accessNotificationGranted.WatchlistMemberFullName,
                WatchlistMemberExternalId = accessNotificationGranted.WatchlistMemberExternalId,
                WatchlistExternalId = accessNotificationGranted.WatchlistExternalId,
                WatchlistFullName = accessNotificationGranted.WatchlistFullName,
                WatchlistId = accessNotificationGranted.WatchlistId,
                MatchResultScore = accessNotificationGranted.MatchResultScore,
                CropImage = accessNotificationGranted.CropImage.ToByteArray(),
                GrpcSentAt = accessNotification.SentAt.ToDateTime(),
                FaceDetectedAt = accessNotification.FaceDetectedAt.ToDateTime()
            };
            return grantedNotification;
        }

        public static BlockedNotification GetBlockedNotification(this AccessNotification accessNotification)
        {
            var accessNotificationBlocked = accessNotification.AccessNotificationBlocked;
            var blockedNotification = new BlockedNotification
            {
                StreamId = accessNotification.StreamId,
                TrackletId = accessNotification.TrackletId,
                FaceId = accessNotification.FaceId,
                WatchlistMemberId = accessNotificationBlocked.WatchlistMemberId,
                WatchlistMemberFullName = accessNotificationBlocked.WatchlistMemberFullName,
                WatchlistFullName = accessNotificationBlocked.WatchlistFullName,
                WatchlistId = accessNotificationBlocked.WatchlistId,
                MatchResultScore = accessNotificationBlocked.MatchResultScore,
                CropImage = accessNotificationBlocked.CropImage.ToByteArray(),
                GrpcSentAt = accessNotification.SentAt.ToDateTime(),
                FaceDetectedAt = accessNotification.FaceDetectedAt.ToDateTime()
            };
            return blockedNotification;
        }

        public static DeniedNotification GetDeniedNotification(this AccessNotification accessNotification)
        {
            var accessNotificationDenied = accessNotification.AccessNotificationDenied;
            var deniedNotification = new DeniedNotification
            {
                StreamId = accessNotification.StreamId,
                TrackletId = accessNotification.TrackletId,
                FaceId = accessNotification.FaceId,
                CropImage = accessNotificationDenied.CropImage.ToByteArray(),
                GrpcSentAt = accessNotification.SentAt.ToDateTime(),
                FaceDetectedAt = accessNotification.FaceDetectedAt.ToDateTime()
            };
            return deniedNotification;
        }
    }
}
