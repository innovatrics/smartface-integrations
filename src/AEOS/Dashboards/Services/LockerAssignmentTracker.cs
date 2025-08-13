using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AeosDashboards.Services;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.Services
{
    public interface ILockerAssignmentTracker
    {
        Task ProcessLockerAssignmentsAsync(IList<LockerGroupAnalytics> currentGroups);
    }

    public class LockerAssignmentTracker : ILockerAssignmentTracker
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly string _groupNameToMonitor;
        private readonly bool _notificationsEnabled;
        private readonly string _adminNotificationEmail;
        
        // Track previous assignments: Dictionary<LockerId, AssignedEmployeeId>
        private readonly Dictionary<long, long?> _previousAssignments = new Dictionary<long, long?>();
        private readonly Dictionary<long, AeosMember> _employeeCache = new Dictionary<long, AeosMember>();

        public LockerAssignmentTracker(ILogger logger, IConfiguration configuration, IEmailService emailService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));

            _notificationsEnabled = _configuration.GetValue<bool>("AeosDashboards:LockerAssignmentNotifications:Enabled", false);
            _groupNameToMonitor = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:GroupNameToMonitor");
            _adminNotificationEmail = _configuration.GetValue<string>("AeosDashboards:LockerAssignmentNotifications:AdminNotificationEmail");

            if (_notificationsEnabled && string.IsNullOrEmpty(_groupNameToMonitor))
            {
                _logger.Warning("Locker assignment notifications are enabled but GroupNameToMonitor is not configured");
            }
        }

        public async Task ProcessLockerAssignmentsAsync(IList<LockerGroupAnalytics> currentGroups)
        {
            if (!_notificationsEnabled || string.IsNullOrEmpty(_groupNameToMonitor))
            {
                return;
            }

            try
            {
                // Find the group to monitor
                var targetGroup = currentGroups.FirstOrDefault(g => 
                    string.Equals(g.Name, _groupNameToMonitor, StringComparison.OrdinalIgnoreCase));

                if (targetGroup == null)
                {
                    _logger.Warning("Target locker group '{GroupName}' not found in current data", _groupNameToMonitor);
                    return;
                }

                // Process each locker in the target group
                foreach (var locker in targetGroup.AllLockers)
                {
                    await ProcessLockerAssignmentAsync(locker, targetGroup);
                }

                // Check for group capacity alerts
                await CheckGroupCapacityAlertsAsync(targetGroup);

                _logger.Debug("Processed {LockerCount} lockers for assignment tracking in group '{GroupName}'", 
                    targetGroup.AllLockers.Count, _groupNameToMonitor);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing locker assignments for group '{GroupName}'", _groupNameToMonitor);
            }
        }

        private async Task ProcessLockerAssignmentAsync(LockerInfo currentLocker, LockerGroupAnalytics group)
        {
            var currentAssignment = currentLocker.AssignedTo;
            var previousAssignment = _previousAssignments.GetValueOrDefault(currentLocker.Id);

            // Check if this is a new assignment (was not assigned before, but is assigned now)
            if (!previousAssignment.HasValue && currentAssignment.HasValue)
            {
                _logger.Information("New locker assignment detected: Locker {LockerName} (ID: {LockerId}) assigned to employee ID {EmployeeId}", 
                    currentLocker.Name, currentLocker.Id, currentAssignment.Value);

                // Get employee details
                var employee = await GetEmployeeDetailsAsync(currentAssignment.Value);
                if (employee != null && !string.IsNullOrEmpty(employee.Email))
                {
                    await _emailService.SendLockerAssignmentNotificationAsync(
                        employee.Email,
                        $"{employee.FirstName} {employee.LastName}",
                        currentLocker.Name,
                        group.Name,
                        currentLocker.AssignedEmployeeIdentifier
                    );
                }
                else
                {
                    _logger.Warning("Cannot send notification for locker {LockerName}: Employee not found or has no email address", 
                        currentLocker.Name);
                }
            }
            // Check if this is an unassignment (was assigned before, but is not assigned now)
            else if (previousAssignment.HasValue && !currentAssignment.HasValue)
            {
                _logger.Information("Locker unassignment detected: Locker {LockerName} (ID: {LockerId}) unassigned from employee ID {EmployeeId}", 
                    currentLocker.Name, currentLocker.Id, previousAssignment.Value);

                // Get employee details for unassignment notification
                var employee = await GetEmployeeDetailsAsync(previousAssignment.Value);
                if (employee != null && !string.IsNullOrEmpty(employee.Email))
                {
                    await _emailService.SendLockerUnassignedNotificationAsync(
                        employee.Email,
                        $"{employee.FirstName} {employee.LastName}",
                        currentLocker.Name,
                        group.Name
                    );
                }
            }

            // Update the previous assignment record
            _previousAssignments[currentLocker.Id] = currentAssignment;
        }

        private async Task CheckGroupCapacityAlertsAsync(LockerGroupAnalytics group)
        {
            // Check if group is full (100% assignment rate)
            if (group.AssignmentPercentage >= 100.0)
            {
                _logger.Information("Locker group '{GroupName}' is at full capacity ({AssignmentPercentage:F1}%)", 
                    group.Name, group.AssignmentPercentage);

                if (!string.IsNullOrEmpty(_adminNotificationEmail))
                {
                    await _emailService.SendLockerGroupFullNotificationAsync(
                        _adminNotificationEmail,
                        group.Name,
                        group.TotalLockers,
                        group.AssignedLockers,
                        group.AssignmentPercentage
                    );
                }
            }
        }

        private async Task<AeosMember> GetEmployeeDetailsAsync(long employeeId)
        {
            // Check cache first
            if (_employeeCache.TryGetValue(employeeId, out var cachedEmployee))
            {
                return cachedEmployee;
            }

            // This would need to be implemented to get employee details from AEOS
            // For now, we'll return null and log a warning
            _logger.Warning("Employee details retrieval not implemented for employee ID {EmployeeId}", employeeId);
            return null;
        }

        // Method to update employee cache (called from DataOrchestrator)
        public void UpdateEmployeeCache(IList<AeosMember> employees)
        {
            _employeeCache.Clear();
            foreach (var employee in employees)
            {
                _employeeCache[employee.Id] = employee;
            }
            _logger.Debug("Updated employee cache with {EmployeeCount} employees", employees.Count);
        }
    }
} 