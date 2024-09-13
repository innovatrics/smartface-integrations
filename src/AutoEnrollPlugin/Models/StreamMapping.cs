using System;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models
{
    public class StreamMapping
    {
        public string StreamGroupId { get; set; }
        public Guid StreamId { get; set; }
        public string[] WatchlistIds { get; set; }

        public Range<double?> FaceSize { get; set; }
        public Range<double?> FaceArea { get; set; }
        public Range<double?> FaceOrder { get; set; }
        public Range<double?> FacesOnFrameCount { get; set; }

        public Range<int?> FaceQuality { get; set; }
        public Range<int?> TemplateQuality { get; set; }

        public Range<double?> Brightness { get; set; }
        public Range<double?> Sharpness { get; set; }

        public Range<double?> YawAngle { get; set; }
        public Range<double?> PitchAngle { get; set; }
        public Range<double?> RollAngle { get; set; }

        public bool KeepAutoLearn { get; set; }

        public int? StreamDebounceMs { get; set; }
        public int? TrackletDebounceMs { get; set; } = 4000;
        public int? GroupDebounceMs { get; set; }
    }

    public class Range<T>
    {
        public T Min { get; set; }
        public T Max { get; set; }
    }
}
