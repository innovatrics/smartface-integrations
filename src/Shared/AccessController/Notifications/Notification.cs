using System;

namespace Innovatrics.SmartFace.Integrations.AccessController.Notifications
{
    public class Notification
    {
        public string TrackletId { get; set; }
        public string StreamId { get; set; }
        public DateTime GrpcSentAt { get; set; }
        public byte[] CropImage { get; set; }
    }
}
