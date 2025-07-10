using System;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class AggregationSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double AveragePedestrians { get; set; }
        public double AverageFaces { get; set; }
        public double AverageIdentifications { get; set; }
    }
} 