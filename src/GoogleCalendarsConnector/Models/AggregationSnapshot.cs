using System;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class AggregationSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int AveragePedestrians { get; set; }
        public int AverageFaces { get; set; }
    }
} 