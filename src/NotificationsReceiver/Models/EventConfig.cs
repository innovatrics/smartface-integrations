using System;

namespace Innovatrics.SmartFace.Integrations.NotificationsReceiver.Models
{
    public class EventConfig
    {
        public string Topic                                 { get; set; }
        public string Caption                               { get; set; }
        public int? DebounceMs                              { get; set; }
    }
}
