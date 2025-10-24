using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.DataModels
{
    public class EmailSummaryResponse
    {
        public DateTime LastCheckTime { get; set; }
        public DateTime CurrentCheckTime { get; set; }
        public List<AssignmentChange> Changes { get; set; } = new List<AssignmentChange>();
        public int TotalChanges { get; set; }
    }

    public class AssignmentChange
    {
        public int LockerId { get; set; }
        public string LockerName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public int? PreviousAssignedTo { get; set; }
        public string PreviousAssignedEmployeeName { get; set; } = string.Empty;
        public string PreviousAssignedEmployeeIdentifier { get; set; } = string.Empty;
        public string PreviousAssignedEmployeeEmail { get; set; } = string.Empty;
        public int? NewAssignedTo { get; set; }
        public string NewAssignedEmployeeName { get; set; } = string.Empty;
        public string NewAssignedEmployeeIdentifier { get; set; } = string.Empty;
        public string NewAssignedEmployeeEmail { get; set; } = string.Empty;
        public DateTime ChangeTimestamp { get; set; }
        public string ChangeType { get; set; } = string.Empty;
    }
}
