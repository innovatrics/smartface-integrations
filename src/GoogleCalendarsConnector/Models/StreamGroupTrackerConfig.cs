using System;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class StreamGroupTrackerConfig
    {
        public int WindowMinutes { get; set; } = 5;
        public int MinPedestrians { get; set; } = 3;
        public int MinFaces { get; set; } = 2;
        public TimeSpan Window => TimeSpan.FromMinutes(WindowMinutes);
    }
} 