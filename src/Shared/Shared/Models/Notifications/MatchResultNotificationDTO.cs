using System;

namespace Innovatrics.SmartFace.Models.Notifications
{
    public class MatchResultNotificationDTO
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public string WatchlistMemberId { get; set; }
        public Guid StreamId { get; set; }
        public int Score { get; set; }
        public Guid? FrameId { get; set; }
        public long FrameTimestampMicroseconds { get; set; }
        public float FaceArea { get; set; }
        public int FaceOrder { get; set; }
        public int FacesOnFrameCount { get; set; }
        public double? FaceMaskConfidence { get; set; }
        public double? NoseTipConfidence { get; set; }
        public string FaceMaskStatus { get; set; }
        public int TemplateQuality { get; set; }
        public double? YawAngle { get; set; }
        public double? PitchAngle { get; set; }
        public double? RollAngle { get; set; }
        public double FaceAreaChange { get; set; }
        public double LeftEyeX { get; set; }
        public double LeftEyeY { get; set; }
        public double RightEyeX { get; set; }
        public double RightEyeY { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid TrackletId { get; set; }
        public Guid FaceId { get; set; }
        public string WatchlistMemberFullName { get; set; }
        public string WatchlistMemberDisplayName { get; set; }
        public string WatchlistFullName { get; set; }
        public string WatchlistDisplayName { get; set; }
        public string WatchlistId { get; set; }
        public string PreviewColor { get; set; }
        public byte[] CropImage { get; set; }
    }
}
