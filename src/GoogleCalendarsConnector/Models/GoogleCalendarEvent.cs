using System;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class GoogleCalendarEvent
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string[] Attendees { get; set; }
        
        // Cache-related properties
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsExpired()
        {
            return DateTime.UtcNow > ExpiresAt;
        }
    }
}