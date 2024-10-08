﻿using System;

namespace SmartFace.AutoEnrollment.Models
{
    public class NoMatchResult
    {
        public string StreamId { get; set; }
        public string FaceId { get; set; }
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

    public class NoMatchResultResponse
    {
        public NoMatchResult NoMatchResult { get; set; }
    }
}
