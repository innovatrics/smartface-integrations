using System;

namespace Innovatrics.SmartFace.StoreNotifications.Data
{
    public class PedestrianProcessed
    {
        public long Id { get; set; }
        
        public string StreamId { get; set; }
        public string TrackletId { get; set; }

        public string FrameId { get; set; }
        
        public long FrameTimestampMicroseconds { get; set; }
        
        public DateTime ProcessedAt { get; set; }

        public double Size { get; set; }
        public double Quality { get; set; }
        
        public int PedestrianOrder { get; set; }
        public int PedestriansOnFrameCount { get; set; }
    }
}
