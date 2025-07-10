using System;
using System.Collections.Generic;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class StreamGroupAggregation
    {
        public StreamInformation StreamInformation { get; set; }
        public FrameInformation FrameInformation { get; set; }
        public List<FrameObject> Objects { get; set; } = new();
        public List<ObjectGroup> ObjectGroups { get; set; } = new();
    }

    public class StreamInformation
    {
        public string StreamId { get; set; }
        public string ClientId { get; set; }
    }

    public class FrameInformation
    {
        public string Id { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime Timestamp { get; set; }
        public string ImageDataId { get; set; }
    }

    public class FrameObject
    {
        public string Id { get; set; }
        public string TrackletId { get; set; }
        public Attribute[] Attributes { get; set; }
        public DetectionInfo DetectionInfo { get; set; }
        public IdentificationResult IdentificationResult { get; set; }
    }

    public class Attribute
    {
        public string Type { get; set; }
    }

    public class DetectionInfo
    {
        public string Type { get; set; }
        public string CropImage { get; set; }
        public int Confidence { get; set; }
    }

    public class IdentificationResult
    {
        public string IdentificationEventType { get; set; }
        public string Modality { get; set; }
        public MemberDetails MemberDetails { get; set; }
    }

    public class MemberDetails
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string FullName { get; set; }
        public List<Label> Labels { get; set; } = new();
        public List<MatchedWatchlist> MatchedWatchlists { get; set; } = new();
    }

    public class Label
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class MatchedWatchlist
    {
        public string Id { get; set; }
        public string FullName { get; set; }
    }

    public class ObjectGroup
    {
        public List<string> ObjectIds { get; set; } = new();
    }
} 