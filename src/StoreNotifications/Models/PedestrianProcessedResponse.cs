using System;

namespace Innovatrics.SmartFace.StoreNotifications.Models
{
    public class PedestrianProcessedNotification
    {
        public FrameInformation FrameInformation { get; set; }
        public PedestrianInformation PedestrianInformation { get; set; }
    }

    public class PedestrianProcessedResponse
    {
        public PedestrianProcessedNotification PedestrianProcessed { get; set; }
    }

    public class FrameInformation
    {
        public string StreamId { get; set; }
        public string FrameId { get; set; }
        public long FrameTimestampMicroseconds { get; set; }
        
        public DateTime ProcessedAt { get; set; }
    }

    public class PedestrianInformation
    {
        public string TrackletId { get; set; }

        public double Size { get; set; }
        public double Quality { get; set; }
        
        public int PedestrianOrder { get; set; }
        public int PedestriansOnFrameCount { get; set; }
    }
}
