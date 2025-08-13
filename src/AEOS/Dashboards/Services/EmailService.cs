using System;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AeosDashboards.Models;
using System.Net.Security;
using System.Security.Authentication;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.Services
{
    public interface IEmailService
    {
        Task SendLockerAssignmentNotificationAsync(string toEmail, string employeeName, string lockerName, string groupName, string assignedEmployeeIdentifier = null);
        Task SendDailyReminderAsync(string toEmail, string groupName, int totalLockers, int assignedLockers, int unassignedLockers, double assignmentPercentage);
        Task SendLockerUnassignedNotificationAsync(string toEmail, string employeeName, string lockerName, string groupName);
        Task SendLockerGroupFullNotificationAsync(string toEmail, string groupName, int totalLockers, int assignedLockers, double assignmentPercentage);
        Task SendMaintenanceNotificationAsync(string toEmail, string groupName, int totalLockers);
        Task SendCustomNotificationAsync(string toEmail, EmailTemplateType templateType, EmailTemplateData templateData);
    }

    public class EmailService : IEmailService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailTemplateService _templateService;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly bool _useSsl;
        private readonly string _username;
        private readonly string _password;
        private readonly string _fromAddress;
        private readonly string _fromName;

        public EmailService(ILogger logger, IConfiguration configuration, IEmailTemplateService templateService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));

            _smtpServer = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:Email:SmtpServer");
            _smtpPort = _configuration.GetValue<int>("AeosDashboards:LockerAssignmentNotifications:Email:SmtpPort", 587);
            _useSsl = _configuration.GetValue<bool>("AeosDashboards:LockerAssignmentNotifications:Email:UseSsl", true);
            _username = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:Email:Username");
            _password = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:Email:Password");
            _fromAddress = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:Email:FromAddress");
            _fromName = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:Email:FromName");
        }

        public async Task SendLockerAssignmentNotificationAsync(string toEmail, string employeeName, string lockerName, string groupName, string assignedEmployeeIdentifier = null)
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.Warning("Cannot send email notification: recipient email is null or empty");
                return;
            }

            var templateData = _templateService.CreateTemplateData(employeeName, toEmail, lockerName, groupName, DateTime.Now, assignedEmployeeIdentifier);
            await SendCustomNotificationAsync(toEmail, EmailTemplateType.LockerAssignment, templateData);
        }

        public async Task SendDailyReminderAsync(string toEmail, string groupName, int totalLockers, int assignedLockers, int unassignedLockers, double assignmentPercentage)
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.Warning("Cannot send daily reminder: recipient email is null or empty");
                return;
            }

            var templateData = _templateService.CreateDailyReminderData(groupName, totalLockers, assignedLockers, unassignedLockers, assignmentPercentage);
            await SendCustomNotificationAsync(toEmail, EmailTemplateType.DailyReminder, templateData);
        }

        public async Task SendLockerUnassignedNotificationAsync(string toEmail, string employeeName, string lockerName, string groupName)
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.Warning("Cannot send unassignment notification: recipient email is null or empty");
                return;
            }

            var templateData = _templateService.CreateTemplateData(employeeName, toEmail, lockerName, groupName, DateTime.Now);
            await SendCustomNotificationAsync(toEmail, EmailTemplateType.LockerUnassigned, templateData);
        }

        public async Task SendLockerGroupFullNotificationAsync(string toEmail, string groupName, int totalLockers, int assignedLockers, double assignmentPercentage)
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.Warning("Cannot send group full notification: recipient email is null or empty");
                return;
            }

            var templateData = _templateService.CreateDailyReminderData(groupName, totalLockers, assignedLockers, 0, assignmentPercentage);
            await SendCustomNotificationAsync(toEmail, EmailTemplateType.LockerGroupFull, templateData);
        }

        public async Task SendMaintenanceNotificationAsync(string toEmail, string groupName, int totalLockers)
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.Warning("Cannot send maintenance notification: recipient email is null or empty");
                return;
            }

            var templateData = _templateService.CreateDailyReminderData(groupName, totalLockers, 0, totalLockers, 0);
            await SendCustomNotificationAsync(toEmail, EmailTemplateType.MaintenanceNotification, templateData);
        }

        public async Task SendCustomNotificationAsync(string toEmail, EmailTemplateType templateType, EmailTemplateData templateData)
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.Warning("Cannot send custom notification: recipient email is null or empty");
                return;
            }

            try
            {
                var template = _templateService.GetTemplate(templateType);
                var subject = _templateService.RenderSubject(template, templateData);
                var body = _templateService.RenderTemplate(template, templateData);

                _logger.Debug("Attempting to send email via SMTP: {SmtpServer}:{SmtpPort}, SSL: {UseSsl}", 
                    _smtpServer, _smtpPort, _useSsl);

                using var client = new SmtpClient(_smtpServer, _smtpPort)
                {
                    EnableSsl = _useSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Timeout = 10000
                };

                // Configure SSL/TLS for Gmail
                if (_useSsl && (_smtpPort == 465 || _smtpPort == 587))
                {
                    client.EnableSsl = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }

                // Set credentials
                if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
                {
                    client.Credentials = new NetworkCredential(_username, _password);
                }

                var message = new MailMessage
                {
                    From = new MailAddress(_fromAddress, _fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = template.IsHtml
                };

                message.To.Add(toEmail);

                await client.SendMailAsync(message);

                _logger.Information("Email notification sent successfully to {Email} using template {TemplateType}", 
                    toEmail, templateType);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send email notification to {Email} using template {TemplateType}. SMTP Server: {SmtpServer}:{SmtpPort}, SSL: {UseSsl}", 
                    toEmail, templateType, _smtpServer, _smtpPort, _useSsl);
            }
        }
    }
} 