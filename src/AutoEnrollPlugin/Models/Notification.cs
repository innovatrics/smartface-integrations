using System;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models
{
    public class Notification22
    {
        public string FaceId { get; set; }
        public string TrackletId { get; set; }
        public string StreamId { get; set; }
        public DateTime GrpcSentAt { get; set; }
        public DateTime? FaceDetectedAt { get; set; }
        public byte[] CropImage { get; set; }
    }
}
