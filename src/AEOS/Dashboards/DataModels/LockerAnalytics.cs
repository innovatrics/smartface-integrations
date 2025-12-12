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
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Function { get; set; } = string.Empty;
        public long Template { get; set; }
        public long? LockerAuthorisationGroupId { get; set; }
        public int TotalLockers { get; set; }
        public int AssignedLockers { get; set; }
        public int UnassignedLockers { get; set; }
        public double AssignmentPercentage { get; set; }
        public List<LockerAuthorisationPresetInfo> AvailablePresets { get; set; } = new List<LockerAuthorisationPresetInfo>();
        public List<LockerInfo> AllLockers { get; set; } = new List<LockerInfo>();
        public List<LockerInfo> LeastUsedLockers { get; set; } = new List<LockerInfo>();
    }

    public class LockerAuthorisationPresetInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Claimable { get; set; }
        public bool Releasable { get; set; }
    }

    public class LockerInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? LastUsed { get; set; }
        public long? AssignedTo { get; set; }
        public string? AssignedEmployeeName { get; set; }
        public string? AssignedEmployeeIdentifier { get; set; }
        public string? AssignedEmployeeEmail { get; set; }
        public double DaysSinceLastUse { get; set; }
    }

    public class LockerAssignmentChange
    {
        public long LockerId { get; set; }
        public string? LockerName { get; set; }
        public string? GroupName { get; set; }
        public long? PreviousAssignedTo { get; set; }
        public string? PreviousAssignedEmployeeName { get; set; }
        public string? PreviousAssignedEmployeeIdentifier { get; set; }
        public string? PreviousAssignedEmployeeEmail { get; set; }
        public long? NewAssignedTo { get; set; }
        public string? NewAssignedEmployeeName { get; set; }
        public string? NewAssignedEmployeeIdentifier { get; set; }
        public string? NewAssignedEmployeeEmail { get; set; }
        public DateTime ChangeTimestamp { get; set; }
        public string ChangeType { get; set; } = string.Empty; // "Assigned", "Unassigned" - each change is a separate event
    }

    public class AssignmentChangesResponse
    {
        public DateTime LastCheckTime { get; set; }
        public DateTime CurrentCheckTime { get; set; }
        public List<LockerAssignmentChange> Changes { get; set; } = new List<LockerAssignmentChange>();
        public int TotalChanges { get; set; }
    }

    public class SimplifiedLockerInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? LastUsed { get; set; }
        public double DaysSinceLastUse { get; set; }
    }

    public class EmployeeAssignmentSummary
    {
        public long EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string? EmployeeEmail { get; set; }
        public string? EmployeeIdentifier { get; set; }
        public List<SimplifiedLockerInfo> AssignedLockers { get; set; } = new List<SimplifiedLockerInfo>();
        public int TotalAssignedLockers { get; set; }
    }

    public class GroupAssignmentEmailSummary
    {
        public long GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string GroupDescription { get; set; } = string.Empty;
        public List<EmployeeAssignmentSummary> EmployeeAssignments { get; set; } = new List<EmployeeAssignmentSummary>();
        public int TotalEmployeesWithAssignments { get; set; }
        public int TotalAssignedLockers { get; set; }
    }
} 