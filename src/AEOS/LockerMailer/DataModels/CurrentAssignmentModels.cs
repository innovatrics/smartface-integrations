using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.DataModels
{
    public class GroupsResponse
    {
        public List<GroupInfo> Groups { get; set; } = new List<GroupInfo>();
    }

    public class GroupInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Function { get; set; } = string.Empty;
        public int Template { get; set; }
        public int TotalLockers { get; set; }
        public int AssignedLockers { get; set; }
        public int UnassignedLockers { get; set; }
        public double AssignmentPercentage { get; set; }
        public List<LockerInfo> AllLockers { get; set; } = new List<LockerInfo>();
        public List<LockerInfo> LeastUsedLockers { get; set; } = new List<LockerInfo>();
    }

    public class LockerInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? LastUsed { get; set; }
        public int? AssignedTo { get; set; }
        public string AssignedEmployeeName { get; set; } = string.Empty;
        public string AssignedEmployeeIdentifier { get; set; } = string.Empty;
        public string AssignedEmployeeEmail { get; set; } = string.Empty;
        public double DaysSinceLastUse { get; set; }
    }
}
