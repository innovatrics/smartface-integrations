using System;
using Innovatrics.SmartFace.Integrations.Shared.Enums;

namespace Innovatrics.SmartFace.Integrations.AccessController.Notifications
{
    public class MatchNotification
    {
        public string WatchlistDisplayName { get; set; }
        public string WatchlistFullName { get; set; }
        public string WatchlistMemberDisplayName { get; set; }
        public string WatchlistMemberFullName { get; set; }
        public Guid FaceId { get; set; }
        public Guid TrackletId { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public double FaceAreaChange { get; set; }
        public FaceMaskStatus FaceMaskStatus { get; set; }
        public double? NoseTipConfidence { get; set; }
        public double? FaceMaskConfidence { get; set; }
        public int FacesOnFrameCount { get; set; }
        public int FaceOrder { get; set; }
        public float FaceArea { get; set; }
        public Guid? FrameId { get; set; }
        public int Score { get; set; }
        public Guid StreamId { get; set; }
        public string WatchlistMemberId { get; set; }
        public MatchResultType Type { get; set; }
        public Guid Id { get; set; }
        public string WatchlistId { get; set; }
        public byte[] CropImage { get; set; }
    }
}
