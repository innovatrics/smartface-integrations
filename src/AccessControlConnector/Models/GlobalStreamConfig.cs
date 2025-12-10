using System;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Models
{
    public class GlobalStreamConfig
    {
        public bool? FaceModalityEnabled { get; set; }
        public bool? PalmModalityEnabled { get; set; }
        public bool? OpticalCodeModalityEnabled { get; set; }

        public StreamConfig[] Streams { get; set; }
        public string StreamsConfigPath { get; set; }
    }
}
