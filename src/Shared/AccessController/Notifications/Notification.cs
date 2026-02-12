using System;
using System.Diagnostics;

namespace Innovatrics.SmartFace.Integrations.AccessController.Notifications
{
    public class Notification
    {
        public ActivityContext ActivityContext { get; set; }
        public string TrackletId { get; set; }
        public string StreamId { get; set; }
        public string FaceId { get; set; }
        public DateTime GrpcSentAt { get; set; }
        public byte[] CropImage { get; set; }
        public Modality Modality { get; set; }
    }
}
