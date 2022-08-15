using System;
using SmartFace.Contract.Models.Enums;

namespace SmartFace.Contract.Models.Notifications
{
    public class NoMatchResultNotificationDTO
    {
        public DateTime CreatedAt { get; set; }
        public Guid? FrameId { get; set; }
        public Guid StreamId { get; set; }
        public Guid FaceId { get; set; }
        public Guid TrackletId { get; set; }
        public FaceMaskStatus FaceMaskStatus { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public double? NoseTipConfidence { get; set; }
        public double FaceAreaChange { get; set; }
        public int FacesOnFrameCount { get; set; }
        public int FaceOrder { get; set; }
        public float FaceArea { get; set; }
        public MatchResultType Type { get; set; }
        public Guid Id { get; set; }
        public double? FaceMaskConfidence { get; set; }
        public byte[] CropImage { get; set; }
    }
}