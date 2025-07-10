using System;
using System.Collections.Generic;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class StreamGroupAggregation
    {
        public string StreamGroupName { get; set; }
        public DateTime Timestamp { get; set; }

        public int TotalFrames { get; set; }

        public int TotalObjects { get; set; }
        public int TotalFaces { get; set; }
        public int TotalPedestrians { get; set; }
        public int TotalIdentifications { get; set; }

        public double AverageObjects { get; set; }
        public int MaxObjects { get; set; }

        public double AverageFaces { get; set; }
        public int MaxFaces { get; set; }

        public double AveragePedestrians { get; set; }
        public int MaxPedestrians { get; set; }

        public double AverageIdentifications { get; set; }
        public int MaxIdentifications { get; set; }
    }
}