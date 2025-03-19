using System;

namespace Innovatrics.SmartFace.StoreNotifications.Models
{
    public class MatchResultNotification
    {
        public string StreamId { get; set; }
        public string FrameId { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string TrackletId { get; set; }
        public string WatchlistId { get; set; }
        public string WatchlistMemberId { get; set; }
        public string WatchlistMemberDisplayName { get; set; }
        public double FaceSize { get; set; }
        public int FaceOrder { get; set; }
        public int FacesOnFrameCount { get; set; }
        public double FaceQuality { get; set; }
    }

    public class MatchResultResponse
    {
        public MatchResultNotification MatchResult { get; set; }
    }
}
