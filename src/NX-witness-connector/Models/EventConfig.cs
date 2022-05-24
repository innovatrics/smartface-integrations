using System;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector.Models
{
    public class EventConfig
    {
        public string Topic                                 { get; set; }
        public string Caption                               { get; set; }
        public int? DebounceMs                              { get; set; }
    }
}
