using System;

namespace Innovatrics.SmartFace.StoreNotifications.Data
{
    public class MatchResult
    {
        public long Id { get; set; }
        
        public string StreamId { get; set; }
        public string FrameId { get; set; }
        public string TrackletId { get; set; }
        public string WatchlistId { get; set; }
        public string WatchlistMemberId { get; set; }
        public string WatchlistMemberDisplayName { get; set; }
        public double FaceSize { get; set; }
        public int FaceOrder { get; set; }
        public int FacesOnFrameCount { get; set; }
        public double FaceQuality { get; set; }
    }
}
