using System;

namespace Innovatrics.SmartFace.Models.Notifications
{
    public class HumanFallDetectionNotificationDTO
    {
        public int Score { get; set; }

        public Guid ObjectId { get; set; }

        public Guid StreamId { get; set; }

        public DateTime FrameTimestamp { get; set; }
    }
}
