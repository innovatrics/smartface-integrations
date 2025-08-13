using System;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.Models
{
    public enum EmailTemplateType
    {
        LockerAssignment,
        DailyReminder,
        LockerUnassigned,
        LockerGroupFull,
        MaintenanceNotification
    }

    public class EmailTemplate
    {
        public EmailTemplateType Type { get; set; }
        public string SubjectTemplate { get; set; }
        public string BodyTemplate { get; set; }
        public bool IsHtml { get; set; }
    }

    public class EmailTemplateData
    {
        public string EmployeeName { get; set; }
        public string EmployeeEmail { get; set; }
        public string LockerName { get; set; }
        public string GroupName { get; set; }
        public DateTime AssignmentDate { get; set; }
        public string AssignedEmployeeIdentifier { get; set; }
        public int TotalLockersInGroup { get; set; }
        public int AssignedLockersInGroup { get; set; }
        public int UnassignedLockersInGroup { get; set; }
        public double AssignmentPercentage { get; set; }
        public string CompanyName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
    }
} 