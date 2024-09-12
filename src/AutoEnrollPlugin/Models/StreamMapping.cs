using System;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models
{
    public class StreamMapping
    {
        public Guid StreamId { get; set; }

        public int? MinDetectionQuality { get; set; }
        public int? MinExtractionQuality { get; set; }

        public double? MinYawAngle { get; set; }
        public double? MaxYawAngle { get; set; }

        public double? MinPitchAngle { get; set; }
        public double? MaxPitchAngle { get; set; }

        public double? MinRollAngle { get; set; }
        public double? MaxRollAngle { get; set; }
    }
}
