using System.Linq;
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

        public static FaceGrantedNotification GetGrantedNotification(this AccessNotification accessNotification)
        {
            var accessNotificationGranted = accessNotification.AccessNotificationGranted;
            var grantedNotification = new FaceGrantedNotification
            {
                StreamId = accessNotification.StreamId,
                TrackletId = accessNotification.TrackletId,
                FaceId = accessNotification.FaceId,
                WatchlistMemberId = accessNotificationGranted.WatchlistMemberId,
                WatchlistMemberFullName = accessNotificationGranted.WatchlistMemberFullName,
                WatchlistMemberExternalId = accessNotificationGranted.WatchlistMemberExternalId,
                WatchlistExternalId = accessNotificationGranted.WatchlistExternalId,
                WatchlistFullName = accessNotificationGranted.WatchlistFullName,
                WatchlistMemberLabels = accessNotificationGranted.WatchlistMemberLabels.ToArray(),
                WatchlistId = accessNotificationGranted.WatchlistId,
                MatchResultScore = accessNotificationGranted.MatchResultScore,
                CropImage = accessNotificationGranted.CropImage.ToByteArray(),
                GrpcSentAt = accessNotification.SentAt.ToDateTime(),
                FaceDetectedAt = accessNotification.FaceDetectedAt.ToDateTime()
            };
            return grantedNotification;
        }

        public static FaceBlockedNotification GetBlockedNotification(this AccessNotification accessNotification)
        {
            var accessNotificationBlocked = accessNotification.AccessNotificationBlocked;
            var blockedNotification = new FaceBlockedNotification
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

        public static FaceDeniedNotification GetDeniedNotification(this AccessNotification accessNotification)
        {
            var accessNotificationDenied = accessNotification.AccessNotificationDenied;
            var deniedNotification = new FaceDeniedNotification
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
