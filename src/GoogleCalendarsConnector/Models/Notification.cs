using System;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class Notification
    {
        public string StreamGroupName { get; set; }
        public DateTime Timestamp { get; set; }
        public int AveragePedestrians { get; set; }
        public int AverageFaces { get; set; }
        public int TotalFrames { get; set; }
        public int TotalObjects { get; set; }
        public int TotalIdentifications { get; set; }
    }
} 