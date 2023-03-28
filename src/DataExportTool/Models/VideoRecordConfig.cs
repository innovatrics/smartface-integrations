namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public class VideoRecordConfig
    {
        public int TrackMinEyeDistance { get; set; }
        public int TrackMaxEyeDistance { get; set; }
        public int FaceDiscoveryFrequence { get; set; }
        public int FaceExtractionFrequence { get; set; }
        public int DetectionThreshold { get; set; }
        public FaceSaveStrategy FaceSaveStrategy { get; set; }
    }
}