using System.Collections.Generic;
using Newtonsoft.Json;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.AnalyticsReporting
{
    public class LockerAnalyticsSnapshotPayload
    {
        [JsonProperty(Order = 0)]
        public List<LockerAnalyticsSnapshotGroup> Groups { get; set; } = new List<LockerAnalyticsSnapshotGroup>();
        [JsonProperty(Order = 1)]
        public string SnapshotAt { get; set; } = string.Empty;
    }

    public class LockerAnalyticsSnapshotGroup
    {
        [JsonProperty(Order = 0)]
        public string Name { get; set; } = string.Empty;
        [JsonProperty(Order = 1)]
        public string Function { get; set; } = string.Empty;
        [JsonProperty(Order = 2)]
        public int TotalLockers { get; set; }
        [JsonProperty(Order = 3)]
        public int AssignedLockers { get; set; }
        [JsonProperty(Order = 4)]
        public List<LockerAnalyticsSnapshotLocker> Lockers { get; set; } = new List<LockerAnalyticsSnapshotLocker>();
    }

    public class LockerAnalyticsSnapshotLocker
    {
        [JsonProperty(Order = 0)]
        public string Name { get; set; } = string.Empty;
        [JsonProperty(Order = 1)]
        public string? LastUsed { get; set; }
        [JsonProperty(Order = 2)]
        public string? AssignedEmployeeName { get; set; }
        [JsonProperty(Order = 3)]
        public string? AssignedEmployeeEmail { get; set; }
    }
}
