using System;
using System.Collections.Generic;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public class LockerAnalytics
    {
        public DateTime LastUpdated { get; set; }
        public List<LockerGroupAnalytics> Groups { get; set; } = new List<LockerGroupAnalytics>();
        public List<LockerInfo> GlobalLeastUsedLockers { get; set; } = new List<LockerInfo>();

        public LockerAnalytics()
        {
            LastUpdated = DateTime.Now;
        }

        public void UpdateGlobalLeastUsedLockers()
        {
            // Get all lockers from all groups and sort by last used date
            var allLockers = Groups
                .SelectMany(g => g.AllLockers)
                .OrderBy(l => l.LastUsed ?? DateTime.MaxValue)
                .Take(10)
                .ToList();

            GlobalLeastUsedLockers = allLockers;
        }
    }

    public class LockerGroupAnalytics
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Function { get; set; }
        public long Template { get; set; }
        public int TotalLockers { get; set; }
        public int AssignedLockers { get; set; }
        public int UnassignedLockers { get; set; }
        public double AssignmentPercentage { get; set; }
        public List<LockerInfo> AllLockers { get; set; } = new List<LockerInfo>();
        public List<LockerInfo> LeastUsedLockers { get; set; } = new List<LockerInfo>();
    }

    public class LockerInfo
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public DateTime? LastUsed { get; set; }
        public long? AssignedTo { get; set; }
        public string AssignedEmployeeName { get; set; }
        public double DaysSinceLastUse { get; set; }
    }
} 