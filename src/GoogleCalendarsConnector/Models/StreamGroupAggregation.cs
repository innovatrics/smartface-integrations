using System;
using System.Collections.Generic;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class StreamGroupAggregation
    {
        public string StreamGroupName { get; set; }
        public DateTime Timestamp { get; set; }
        public int TotalFrames { get; set; }
        public int TotalObjects { get; set; }
        public int TotalFaces { get; set; }
        public int TotalPedestrians { get; set; }
        public int TotalIdentifications { get; set; }
        public double AverageObjects { get; set; }
        public int MaxObjects { get; set; }
        public double AverageFaces { get; set; }
        public int MaxFaces { get; set; }
        public double AveragePedestrians { get; set; }
        public int MaxPedestrians { get; set; }
        public double AverageIdentifications { get; set; }
        public int MaxIdentifications { get; set; }
        public List<IdentificationResult> Identifications { get; set; } = new();
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
}