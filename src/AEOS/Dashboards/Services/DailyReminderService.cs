using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AeosDashboards.Services;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.Services
{
    public interface IDailyReminderService
    {
        Task SendDailyReminderForGroupAsync(string groupName);
    }

    public class DailyReminderService : IDailyReminderService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly IDataOrchestrator _dataOrchestrator;
        private readonly string _groupNameToMonitor;
        private readonly bool _notificationsEnabled;
        private readonly string _dailyReminderRecipientEmail;

        public DailyReminderService(
            ILogger logger, 
            IConfiguration configuration, 
            IEmailService emailService,
            IDataOrchestrator dataOrchestrator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _dataOrchestrator = dataOrchestrator ?? throw new ArgumentNullException(nameof(dataOrchestrator));

            _notificationsEnabled = _configuration.GetValue<bool>("AeosDashboards:LockerAssignmentNotifications:Enabled", false);
            _groupNameToMonitor = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:GroupNameToMonitor");
            _dailyReminderRecipientEmail = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:DailyReminderRecipientEmail");

            if (_notificationsEnabled && string.IsNullOrEmpty(_dailyReminderRecipientEmail))
            {
                _logger.Warning("Daily reminder notifications are enabled but DailyReminderRecipientEmail is not configured");
            }
        }

        public async Task SendDailyReminderForGroupAsync(string groupName)
        {
            if (!_notificationsEnabled || string.IsNullOrEmpty(_dailyReminderRecipientEmail))
            {
                _logger.Debug("Daily reminder notifications are disabled or recipient email not configured");
                return;
            }

            try
            {
                // Get current analytics data
                var analytics = await _dataOrchestrator.GetLockerAnalytics();
                var targetGroup = analytics.Groups.FirstOrDefault(g => 
                    string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));

                if (targetGroup == null)
                {
                    _logger.Warning("Target locker group '{GroupName}' not found for daily reminder", groupName);
                    return;
                }

                // Send daily reminder email
                await _emailService.SendDailyReminderAsync(
                    _dailyReminderRecipientEmail,
                    targetGroup.Name,
                    targetGroup.TotalLockers,
                    targetGroup.AssignedLockers,
                    targetGroup.UnassignedLockers,
                    targetGroup.AssignmentPercentage
                );

                _logger.Information("Daily reminder sent successfully for group '{GroupName}' to {Email}", 
                    groupName, _dailyReminderRecipientEmail);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send daily reminder for group '{GroupName}'", groupName);
            }
        }
    }

    public class DailyReminderHostedService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IDailyReminderService _dailyReminderService;
        private readonly string _groupNameToMonitor;
        private readonly bool _notificationsEnabled;
        private readonly TimeSpan _reminderTime;

        public DailyReminderHostedService(
            ILogger logger,
            IConfiguration configuration,
            IDailyReminderService dailyReminderService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _dailyReminderService = dailyReminderService ?? throw new ArgumentNullException(nameof(dailyReminderService));

            _notificationsEnabled = _configuration.GetValue<bool>("AeosDashboards:LockerAssignmentNotifications:Enabled", false);
            _groupNameToMonitor = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:GroupNameToMonitor");
            
            // Default to 6 PM (18:00)
            var reminderHour = _configuration.GetValue<int>("AeosDashboards:LockerAssignmentNotifications:DailyReminderHour", 18);
            var reminderMinute = _configuration.GetValue<int>("AeosDashboards:LockerAssignmentNotifications:DailyReminderMinute", 0);
            _reminderTime = new TimeSpan(reminderHour, reminderMinute, 0);

            if (_notificationsEnabled && string.IsNullOrEmpty(_groupNameToMonitor))
            {
                _logger.Warning("Daily reminder service is enabled but GroupNameToMonitor is not configured");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (!_notificationsEnabled || string.IsNullOrEmpty(_groupNameToMonitor))
            {
                _logger.Information("Daily reminder service is disabled or not configured");
                return;
            }

            _logger.Information("Daily reminder service started. Will send reminders at {ReminderTime} for group '{GroupName}'", 
                _reminderTime, _groupNameToMonitor);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var nextReminder = now.Date.Add(_reminderTime);

                    // If it's already past the reminder time today, schedule for tomorrow
                    if (now.TimeOfDay >= _reminderTime)
                    {
                        nextReminder = nextReminder.AddDays(1);
                    }

                    var delay = nextReminder - now;
                    _logger.Debug("Next daily reminder scheduled for {NextReminder}. Waiting {Delay} hours", 
                        nextReminder, delay.TotalHours);

                    await Task.Delay(delay, cancellationToken);

                    // Send the daily reminder
                    await _dailyReminderService.SendDailyReminderForGroupAsync(_groupNameToMonitor);
                }
                catch (OperationCanceledException)
                {
                    _logger.Information("Daily reminder service is being shut down");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in daily reminder service");
                    // Wait a bit before retrying
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                }
            }
        }
    }
} 