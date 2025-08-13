using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Innovatrics.SmartFace.Integrations.AeosDashboards.Models;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.Services
{
    public interface IEmailTemplateService
    {
        EmailTemplate GetTemplate(EmailTemplateType templateType);
        string RenderTemplate(EmailTemplate template, EmailTemplateData data);
        string RenderSubject(EmailTemplate template, EmailTemplateData data);
        EmailTemplateData CreateTemplateData(string employeeName, string employeeEmail, string lockerName, string groupName, DateTime assignmentDate, string assignedEmployeeIdentifier = null);
        EmailTemplateData CreateDailyReminderData(string groupName, int totalLockers, int assignedLockers, int unassignedLockers, double assignmentPercentage);
    }

    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly IConfiguration _configuration;
        private readonly Dictionary<EmailTemplateType, EmailTemplate> _templates;

        public EmailTemplateService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _templates = InitializeTemplates();
        }

        public EmailTemplate GetTemplate(EmailTemplateType templateType)
        {
            return _templates.TryGetValue(templateType, out var template) ? template : _templates[EmailTemplateType.LockerAssignment];
        }

        public string RenderTemplate(EmailTemplate template, EmailTemplateData data)
        {
            if (template == null || data == null)
                return string.Empty;

            var body = template.BodyTemplate;

            // Replace placeholders with actual data
            body = body.Replace("{{EmployeeName}}", data.EmployeeName ?? "")
                      .Replace("{{EmployeeEmail}}", data.EmployeeEmail ?? "")
                      .Replace("{{LockerName}}", data.LockerName ?? "")
                      .Replace("{{GroupName}}", data.GroupName ?? "")
                      .Replace("{{AssignmentDate}}", data.AssignmentDate.ToString("yyyy-MM-dd HH:mm:ss"))
                      .Replace("{{AssignedEmployeeIdentifier}}", data.AssignedEmployeeIdentifier ?? "")
                      .Replace("{{TotalLockersInGroup}}", data.TotalLockersInGroup.ToString())
                      .Replace("{{AssignedLockersInGroup}}", data.AssignedLockersInGroup.ToString())
                      .Replace("{{UnassignedLockersInGroup}}", data.UnassignedLockersInGroup.ToString())
                      .Replace("{{AssignmentPercentage}}", data.AssignmentPercentage.ToString("F1"))
                      .Replace("{{CompanyName}}", data.CompanyName ?? "Your Company")
                      .Replace("{{ContactEmail}}", data.ContactEmail ?? "facilities@company.com")
                      .Replace("{{ContactPhone}}", data.ContactPhone ?? "+1-555-0123")
                      .Replace("{{CurrentDate}}", DateTime.Now.ToString("yyyy-MM-dd"))
                      .Replace("{{CurrentTime}}", DateTime.Now.ToString("HH:mm:ss"));

            return body;
        }

        public string RenderSubject(EmailTemplate template, EmailTemplateData data)
        {
            if (template == null || data == null)
                return string.Empty;

            var subject = template.SubjectTemplate;

            // Replace placeholders with actual data
            subject = subject.Replace("{{EmployeeName}}", data.EmployeeName ?? "")
                             .Replace("{{EmployeeEmail}}", data.EmployeeEmail ?? "")
                             .Replace("{{LockerName}}", data.LockerName ?? "")
                             .Replace("{{GroupName}}", data.GroupName ?? "")
                             .Replace("{{AssignmentDate}}", data.AssignmentDate.ToString("yyyy-MM-dd HH:mm:ss"))
                             .Replace("{{AssignedEmployeeIdentifier}}", data.AssignedEmployeeIdentifier ?? "")
                             .Replace("{{TotalLockersInGroup}}", data.TotalLockersInGroup.ToString())
                             .Replace("{{AssignedLockersInGroup}}", data.AssignedLockersInGroup.ToString())
                             .Replace("{{UnassignedLockersInGroup}}", data.UnassignedLockersInGroup.ToString())
                             .Replace("{{AssignmentPercentage}}", data.AssignmentPercentage.ToString("F1"))
                             .Replace("{{CompanyName}}", data.CompanyName ?? "Your Company")
                             .Replace("{{ContactEmail}}", data.ContactEmail ?? "facilities@company.com")
                             .Replace("{{ContactPhone}}", data.ContactPhone ?? "+1-555-0123")
                             .Replace("{{CurrentDate}}", DateTime.Now.ToString("yyyy-MM-dd"))
                             .Replace("{{CurrentTime}}", DateTime.Now.ToString("HH:mm:ss"));

            return subject;
        }

        public EmailTemplateData CreateTemplateData(string employeeName, string employeeEmail, string lockerName, string groupName, DateTime assignmentDate, string assignedEmployeeIdentifier = null)
        {
            return new EmailTemplateData
            {
                EmployeeName = employeeName,
                EmployeeEmail = employeeEmail,
                LockerName = lockerName,
                GroupName = groupName,
                AssignmentDate = assignmentDate,
                AssignedEmployeeIdentifier = assignedEmployeeIdentifier,
                CompanyName = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:CompanyName", "Your Company"),
                ContactEmail = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:ContactEmail", "facilities@company.com"),
                ContactPhone = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:ContactPhone", "+1-555-0123")
            };
        }

        public EmailTemplateData CreateDailyReminderData(string groupName, int totalLockers, int assignedLockers, int unassignedLockers, double assignmentPercentage)
        {
            return new EmailTemplateData
            {
                GroupName = groupName,
                TotalLockersInGroup = totalLockers,
                AssignedLockersInGroup = assignedLockers,
                UnassignedLockersInGroup = unassignedLockers,
                AssignmentPercentage = assignmentPercentage,
                CompanyName = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:CompanyName", "Your Company"),
                ContactEmail = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:ContactEmail", "facilities@company.com"),
                ContactPhone = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:ContactPhone", "+1-555-0123")
            };
        }

        private Dictionary<EmailTemplateType, EmailTemplate> InitializeTemplates()
        {
            return new Dictionary<EmailTemplateType, EmailTemplate>
            {
                [EmailTemplateType.LockerAssignment] = new EmailTemplate
                {
                    Type = EmailTemplateType.LockerAssignment,
                    SubjectTemplate = "Locker Assignment Notification - {{LockerName}}",
                    IsHtml = false,
                    BodyTemplate = @"Dear {{EmployeeName}},

You have been assigned a new locker.

Locker Details:
- Locker Name: {{LockerName}}
- Group: {{GroupName}}
- Assignment Date: {{AssignmentDate}}
- Employee Identifier: {{AssignedEmployeeIdentifier}}

Please contact the facilities department if you have any questions about your locker assignment.

Best regards,
{{CompanyName}} Facilities Team
Contact: {{ContactEmail}} | {{ContactPhone}}"
                },

                [EmailTemplateType.DailyReminder] = new EmailTemplate
                {
                    Type = EmailTemplateType.DailyReminder,
                    SubjectTemplate = "Daily Locker Group Status Report - {{GroupName}}",
                    IsHtml = false,
                    BodyTemplate = @"Daily Locker Group Status Report

Group: {{GroupName}}
Date: {{CurrentDate}}
Time: {{CurrentTime}}

Current Status:
- Total Lockers: {{TotalLockersInGroup}}
- Assigned Lockers: {{AssignedLockersInGroup}}
- Unassigned Lockers: {{UnassignedLockersInGroup}}
- Assignment Rate: {{AssignmentPercentage}}%

This is an automated daily reminder for the {{GroupName}} locker group.

For any questions or concerns, please contact:
{{ContactEmail}} | {{ContactPhone}}

Best regards,
{{CompanyName}} Facilities Team"
                },

                [EmailTemplateType.LockerUnassigned] = new EmailTemplate
                {
                    Type = EmailTemplateType.LockerUnassigned,
                    SubjectTemplate = "Locker Unassigned - {{LockerName}}",
                    IsHtml = false,
                    BodyTemplate = @"Dear {{EmployeeName}},

Your locker assignment has been removed.

Locker Details:
- Locker Name: {{LockerName}}
- Group: {{GroupName}}
- Unassignment Date: {{CurrentDate}} {{CurrentTime}}

Please return any keys or access cards to the facilities department.

If you believe this is an error, please contact us immediately.

Best regards,
{{CompanyName}} Facilities Team
Contact: {{ContactEmail}} | {{ContactPhone}}"
                },

                [EmailTemplateType.LockerGroupFull] = new EmailTemplate
                {
                    Type = EmailTemplateType.LockerGroupFull,
                    SubjectTemplate = "Locker Group Full - {{GroupName}}",
                    IsHtml = false,
                    BodyTemplate = @"Locker Group Capacity Alert

Group: {{GroupName}}
Date: {{CurrentDate}}
Time: {{CurrentTime}}

The {{GroupName}} locker group has reached full capacity.

Current Status:
- Total Lockers: {{TotalLockersInGroup}}
- Assigned Lockers: {{AssignedLockersInGroup}}
- Assignment Rate: {{AssignmentPercentage}}%

No new assignments can be made to this group until lockers become available.

Please contact the facilities department if you need assistance with alternative arrangements.

Best regards,
{{CompanyName}} Facilities Team
Contact: {{ContactEmail}} | {{ContactPhone}}"
                },

                [EmailTemplateType.MaintenanceNotification] = new EmailTemplate
                {
                    Type = EmailTemplateType.MaintenanceNotification,
                    SubjectTemplate = "Locker Maintenance Notification - {{GroupName}}",
                    IsHtml = false,
                    BodyTemplate = @"Maintenance Notification

Group: {{GroupName}}
Date: {{CurrentDate}}
Time: {{CurrentTime}}

Scheduled maintenance will be performed on the {{GroupName}} locker group.

Please ensure all personal items are removed from your locker before the maintenance period.

Maintenance Details:
- Group: {{GroupName}}
- Affected Lockers: {{TotalLockersInGroup}}
- Notification Date: {{CurrentDate}}

We apologize for any inconvenience this may cause.

Best regards,
{{CompanyName}} Facilities Team
Contact: {{ContactEmail}} | {{ContactPhone}}"
                }
            };
        }
    }
} 