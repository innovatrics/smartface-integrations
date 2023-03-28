using System;
using System.Runtime.Serialization;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public class CreateVideoRecordResponse
    {
        public string State { get; set; }
        
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Source { get; set; }

        public bool Enabled { get; set; }

        public VideoFaceDetectorConfig FaceDetectorConfig { get; set; }
        
        public string FaceDetectorResourceId { get; set; }

        public string TemplateGeneratorResourceId { get; set; }

        public int RedetectionTime { get; set; }

        public int TemplateGenerationTime { get; set; }

        public string TrackMotionOptimization { get; set; }

        public string FaceSaveStrategy { get; set; }
        
        public string MaskImagePath { get; set; }

        public bool SaveFrameImageData { get; set; }

        public int ImageQuality { get; set; }

        public bool MPEG1PreviewEnabled { get; set; }

        public int MPEG1PreviewPort { get; set; }

        public int MPEG1VideoBitrate { get; set; }

        public int PreviewMaxDimension { get; set; }
    }

    public class VideoFaceDetectorConfig
    {
        public int MinFaceSize { get; set; }
        
        public int MaxFaceSize { get; set; }
        
        public int MaxFaces { get; set; }
        
        public int ConfidenceThreshold { get; set; }
    }
    
    [Flags]
    public enum FaceSaveStrategy 
    {
        /// <summary>
        /// All faces will be saved to database.
        /// </summary>
        All = 0,

        /// <summary>
        /// Only first tracked face and matched faces will be saved to database.
        /// </summary>
        FirstFace = 1,

        /// <summary>
        /// Only best tracked face and matched faces will be saved to database.
        /// </summary>
        BestFace = 2,

        /// <summary>
        /// Only first, best tracked face and matched faces will be saved to database.
        /// </summary>
        [EnumMember(Value = "FirstFace, BestFace")]
        FirstFaceBestFace = 3,

        /// <summary>
        /// Matched faces will be saved to database.
        /// </summary>
        MatchedOnly = 4
    }
}