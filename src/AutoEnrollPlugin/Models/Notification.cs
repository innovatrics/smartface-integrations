using System;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models
{
    public class Notification
    {
        public string StreamId { get; set; }
        public string FaceId { get; set; }
        public string TrackletId { get; set; }
        public byte[] CropImage { get; set; }
    }
}
