using System;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models
{
    public class Config
    {
        public bool ApplyForAllStreams { get; set; }
        public Conditions Conditions { get; set; }
        public string DebugOutputFolder { get; set; }
        public int? MaxParallelActionBlocks { get; set; }
        public int? RegisterMaxFaces { get; set; }
        public int? RegisterMinFaceSize { get; set; }
        public int? RegisterMaxFaceSize { get; set; }
        public int? RegisterFaceConfidence { get; set; }
    }
}
