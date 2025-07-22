using System;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class StreamGroupMapping
    {
        public string GroupName { get; set; }
        public string CalendarId { get; set; }

        public double AveragePedestriansThreshold { get; set; } = 1.0;
    }
}
