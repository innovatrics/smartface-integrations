using System;

namespace SmartFace.AutoEnrollment.Models
{
    public class IdentificationEvent
    {
        public string IdentificationEventType { get; set; }
        public FrameInformation FrameInformation { get; set; }
        public StreamInformation StreamInformation { get; set; }

        public string Modality { get; set; }
        public FaceModalityInfo FaceModalityInfo { get; set; }
    }

    public class FrameInformation
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class StreamInformation
    {
        public string StreamId { get; set; }
    }

    public class FaceModalityInfo
    {
        public FaceInformation FaceInformation { get; set; }
    }

    public class FaceInformation
    {
        public string Id { get; set; }
        public string TrackletId { get; set; }
        public byte[] CropImage { get; set; }

        public double? FaceArea { get; set; }
        public double? FaceSize { get; set; }
        public int? FaceOrder { get; set; }
        public int? FacesOnFrameCount { get; set; }

        public string FaceMaskStatus { get; set; }
        public double? FaceQuality { get; set; }
        public double? TemplateQuality { get; set; 
        }
        public double? Sharpness { get; set; }
        public double? Brightness { get; set; }
        
        public double? YawAngle { get; set; }
        public double? RollAngle { get; set; }
        public double? PitchAngle { get; set; }

        public DateTime? ProcessedAt { get; set; }
    }

    public class IdentificationEventResponse
    {
        public IdentificationEvent IdentificationEvent { get; set; }
    }
}
