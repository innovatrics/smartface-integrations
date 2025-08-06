using System.Collections.Generic;

namespace SmartFace.GoogleCalendarsConnector.Models
{
    public class Config
    {
        public int MaxParallelActionBlocks { get; set; } = 1;
    }
    public class StreamGroupsConfiguration
    {
        public List<StreamGroup> StreamGroups { get; set; } = new List<StreamGroup>();
    }
    public class StreamGroup
    {
        public string Name { get; set; }
        public List<string> StreamIds { get; set; } = new List<string>();
        public int AggregationIntervalMs { get; set; } = 30000;
    }
} 