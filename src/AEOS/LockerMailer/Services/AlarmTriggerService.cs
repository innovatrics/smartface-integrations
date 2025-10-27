using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.Services
{
    public class AlarmTriggerService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IDataOrchestrator dataOrchestrator;
        private readonly IDashboardsDataAdapter dashboardsDataAdapter;
        private readonly Dictionary<string, DateTime> lastTriggeredTimes = new Dictionary<string, DateTime>();
        private readonly List<AlarmConfiguration> alarmConfigurations = new List<AlarmConfiguration>();
        private readonly List<TemplateConfiguration> templateConfigurations = new List<TemplateConfiguration>();

        public AlarmTriggerService(
            ILogger logger,
            IConfiguration configuration,
            IDataOrchestrator dataOrchestrator,
            IDashboardsDataAdapter dashboardsDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.dataOrchestrator = dataOrchestrator ?? throw new ArgumentNullException(nameof(dataOrchestrator));
            this.dashboardsDataAdapter = dashboardsDataAdapter ?? throw new ArgumentNullException(nameof(dashboardsDataAdapter));

            LoadAlarmConfigurations();
            LoadTemplateConfigurations();
        }

        private void LoadAlarmConfigurations()
        {
            try
            {
                var alarmsSection = configuration.GetSection("LockerMailer:Alarms");
                foreach (var alarm in alarmsSection.GetChildren())
                {
                    var alarmName = alarm.GetValue<string>("AlarmName");
                    var alarmTime = alarm.GetValue<string>("AlarmTime");
                    
                    if (!string.IsNullOrEmpty(alarmName) && !string.IsNullOrEmpty(alarmTime))
                    {
                        if (TimeSpan.TryParse(alarmTime, out var timeSpan))
                        {
                            alarmConfigurations.Add(new AlarmConfiguration
                            {
                                AlarmName = alarmName,
                                AlarmTime = timeSpan
                            });
                            logger.Information($"Loaded alarm configuration: {alarmName} at {alarmTime}");
                        }
                        else
                        {
                            logger.Warning($"Invalid time format for alarm {alarmName}: {alarmTime}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load alarm configurations");
            }
        }

        private void LoadTemplateConfigurations()
        {
            try
            {
                var templatesSection = configuration.GetSection("LockerMailer:Templates");
                foreach (var template in templatesSection.GetChildren())
                {
                    var templateId = template.GetValue<string>("templateId");
                    var templateAlarm = template.GetValue<string>("templateAlarm");
                    var templateCheckGroup = template.GetValue<string>("templateCheckGroup");
                    
                    if (!string.IsNullOrEmpty(templateId) && !string.IsNullOrEmpty(templateAlarm))
                    {
                        templateConfigurations.Add(new TemplateConfiguration
                        {
                            TemplateId = templateId,
                            TemplateAlarm = templateAlarm,
                            TemplateCheckGroup = templateCheckGroup ?? string.Empty
                        });
                        logger.Information($"Loaded template configuration: {templateId} with alarm {templateAlarm}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load template configurations");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.Information("AlarmTriggerService started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var currentTime = DateTime.Now;
                    var currentTimeOnly = currentTime.TimeOfDay;

                    // Check each alarm configuration
                    foreach (var alarm in alarmConfigurations)
                    {
                        // Check if it's time for this alarm (within 1 minute tolerance)
                        var timeDifference = Math.Abs((currentTimeOnly - alarm.AlarmTime).TotalMinutes);
                        
                        if (timeDifference <= 1.0) // Within 1 minute
                        {
                            var alarmKey = $"{alarm.AlarmName}_{currentTime.Date:yyyy-MM-dd}";
                            
                            // Check if we've already triggered this alarm today
                            if (!lastTriggeredTimes.ContainsKey(alarmKey) || 
                                lastTriggeredTimes[alarmKey].Date != currentTime.Date)
                            {
                                logger.Information($"Alarm triggered: {alarm.AlarmName} at {currentTime:HH:mm:ss}");
                                
                                // Find templates that use this alarm
                                var templatesToTrigger = templateConfigurations
                                    .Where(t => t.TemplateAlarm.Equals(alarm.AlarmName, StringComparison.OrdinalIgnoreCase))
                                    .ToList();

                    if (templatesToTrigger.Any())
                    {
                        // Check if lockers-flow_3 or lockers-flow_4 is among the triggered templates
                        var lockersFlow3Triggered = templatesToTrigger.Any(t => t.TemplateId.Equals("lockers-flow_3", StringComparison.OrdinalIgnoreCase));
                        var lockersFlow4Triggered = templatesToTrigger.Any(t => t.TemplateId.Equals("lockers-flow_4", StringComparison.OrdinalIgnoreCase));
                        if (lockersFlow3Triggered)
                        {
                            logger.Information("lockers-flow_3 triggered as it is time");
                        }
                        if (lockersFlow4Triggered)
                        {
                            logger.Information("lockers-flow_4 triggered as it is time");
                        }
                        
                        await ProcessAlarmTriggeredTemplates(templatesToTrigger);
                    }
                    else
                    {
                        logger.Warning($"No templates found for alarm: {alarm.AlarmName}");
                    }

                                // Mark this alarm as triggered today
                                lastTriggeredTimes[alarmKey] = currentTime;
                            }
                        }
                    }

                    // Wait 30 seconds before checking again
                    await Task.Delay(30000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    logger.Information("AlarmTriggerService is being shut down");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error in AlarmTriggerService");
                }
            }
        }

        private async Task ProcessAlarmTriggeredTemplates(List<TemplateConfiguration> templates)
        {
            // Step 1: Get current group data via DashboardsDataAdapter
            logger.Information("Step 1: Fetching current group data from Dashboards API");
            List<GroupInfo> groups = new List<GroupInfo>();
            try
            {
                // Add a small random delay to avoid simultaneous API calls with MainHostedService
                var randomDelay = new Random().Next(500, 2000);
                await Task.Delay(randomDelay);
                
                groups = await dashboardsDataAdapter.GetGroups();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to get groups data from Dashboards API");
                logger.Warning("Cannot process alarm-triggered templates without group data");
                return;
            }

            // Step 2: Check current Keila templates (fetch once for all processing)
            logger.Information("Step 2: Fetching Keila campaigns once for all template processing");
            List<KeilaCampaign> keilaCampaigns = new List<KeilaCampaign>();
            try
            {
                // Get Keila campaigns through the data orchestrator
                var keilaDataAdapter = dataOrchestrator.GetType().GetField("keilaDataAdapter", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(dataOrchestrator);
                
                if (keilaDataAdapter != null)
                {
                    var getCampaignsMethod = keilaDataAdapter.GetType().GetMethod("GetCampaignsWithTemplatesAsync");
                    if (getCampaignsMethod != null)
                    {
                        var task = (Task<List<KeilaCampaign>>)getCampaignsMethod.Invoke(keilaDataAdapter, null);
                        keilaCampaigns = await task;
                        logger.Debug($"Successfully retrieved {keilaCampaigns.Count} Keila campaigns for all template processing");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to get Keila campaigns");
                logger.Warning("Cannot process alarm-triggered templates without Keila template data");
                return;
            }

            // Step 3: Process triggered templates with the data
            logger.Information("Step 3: Processing alarm-triggered templates with retrieved data");
            foreach (var template in templates)
            {
                try
                {
                    logger.Debug($"Processing alarm-triggered template: {template.TemplateId}");

                    // Special handling for lockers-flow_3 and lockers-flow_4 to show detailed assignment information
                    if (template.TemplateId.Equals("lockers-flow_3", StringComparison.OrdinalIgnoreCase) || 
                        template.TemplateId.Equals("lockers-flow_4", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Information($"{template.TemplateId} triggered");
                        
                        // Get current assignments for the specific group using the groups data
                        if (!string.IsNullOrEmpty(template.TemplateCheckGroup))
                        {
                            // Find the specific group from the already retrieved groups data
                            var targetGroup = groups.FirstOrDefault(g => g.Name.Equals(template.TemplateCheckGroup, StringComparison.OrdinalIgnoreCase));
                            
                            if (targetGroup != null)
                            {
                                logger.Debug($"Current assignments for group '{template.TemplateCheckGroup}':");
                                logger.Debug($"  Total lockers: {targetGroup.TotalLockers}");
                                logger.Debug($"  Assigned lockers: {targetGroup.AssignedLockers}");
                                logger.Debug($"  Unassigned lockers: {targetGroup.UnassignedLockers}");
                                logger.Debug($"  Assignment percentage: {targetGroup.AssignmentPercentage:F1}%");
                                
                                // Get assigned lockers (those with assignedTo not null)
                                var assignedLockers = targetGroup.AllLockers.Where(l => l.AssignedTo.HasValue && !string.IsNullOrEmpty(l.AssignedEmployeeName)).ToList();
                                
                                if (assignedLockers.Any())
                                {
                                    logger.Debug($"Current locker assignments in '{template.TemplateCheckGroup}' group:");
                                    
                                    // Group lockers by employee
                                    var employeeGroups = assignedLockers.GroupBy(l => l.AssignedTo.Value).ToList();
                                    
                                    // Filter to only employees with email addresses
                                    var employeesWithEmails = employeeGroups.Where(eg => 
                                    {
                                        var firstLocker = eg.First();
                                        return !string.IsNullOrEmpty(firstLocker.AssignedEmployeeEmail);
                                    }).ToList();
                                    
                                    logger.Information($"Found {employeeGroups.Count} employees with assignments, {employeesWithEmails.Count} have email addresses");
                                    
                                    // Only print details for employees with email addresses
                                    if (employeesWithEmails.Any())
                                    {
                                        logger.Information($"Current locker assignments in '{template.TemplateCheckGroup}' group (employees with email addresses):");
                                        
                                        foreach (var employeeGroup in employeesWithEmails)
                                        {
                                            var firstLocker = employeeGroup.First();
                                            logger.Information($"  • Employee: {firstLocker.AssignedEmployeeName} (Email: {firstLocker.AssignedEmployeeEmail}, Assigned lockers: {employeeGroup.Count()})");
                                            
                                            foreach (var locker in employeeGroup)
                                            {
                                                logger.Information($"      - Locker: {locker.Name} (ID: {locker.Id})");
                                                if (locker.LastUsed.HasValue)
                                                {
                                                    logger.Debug($"        Last used: {locker.LastUsed:yyyy-MM-dd HH:mm:ss}");
                                                }
                                                else
                                                {
                                                    logger.Debug($"        Last used: Never");
                                                }
                                                logger.Debug($"        Days since last use: {locker.DaysSinceLastUse:F1}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        logger.Information($"No employees with email addresses found in '{template.TemplateCheckGroup}' group");
                                    }
                                    
                                    // Only process employees with email addresses
                                    if (employeesWithEmails.Any())
                                    {
                                        logger.Information($"Processing {employeesWithEmails.Count} employees with email addresses for template processing");
                                        
                                        foreach (var employeeGroup in employeesWithEmails)
                                        {
                                            var firstLocker = employeeGroup.First();
                                            var allLockerNames = employeeGroup.Select(l => l.Name).ToList();
                                            
                                            // Create a single mock AssignmentChange per employee (not per locker)
                                            // This prevents duplicate Keila API calls and email generation
                                            var mockChange = new AssignmentChange
                                            {
                                                LockerId = firstLocker.Id, // Use first locker ID
                                                LockerName = firstLocker.Name, // Use first locker name
                                                GroupName = template.TemplateCheckGroup,
                                                NewAssignedTo = firstLocker.AssignedTo,
                                                NewAssignedEmployeeName = firstLocker.AssignedEmployeeName,
                                                NewAssignedEmployeeEmail = firstLocker.AssignedEmployeeEmail,
                                                NewAssignedEmployeeIdentifier = firstLocker.AssignedEmployeeIdentifier,
                                                ChangeTimestamp = DateTime.UtcNow,
                                                ChangeType = "CurrentAssignment",
                                                // New properties for lockers-flow_3 template
                                                AllAssignedLockerNames = allLockerNames,
                                                TotalAssignedLockers = employeeGroup.Count()
                                            };
                                            
                                            logger.Information($"Processing template for employee: {firstLocker.AssignedEmployeeName} ({firstLocker.AssignedEmployeeEmail})");
                                            await dataOrchestrator.ProcessTemplateWithAssignmentData(template.TemplateId, mockChange, keilaCampaigns);
                                        }
                                    }
                                    else
                                    {
                                        logger.Information("No employees with email addresses found - skipping template processing");
                                    }
                                }
                                else
                                {
                                    logger.Information($"No current assignments found in '{template.TemplateCheckGroup}' group");
                                }
                            }
                            else
                            {
                                logger.Warning($"Group '{template.TemplateCheckGroup}' not found in groups response");
                            }
                        }
                        else
                        {
                            logger.Warning($"No templateCheckGroup specified for template {template.TemplateId}");
                        }
                    }
                    else
                    {
                        // For other templates, use the original change-based approach
                        var emailSummary = await dashboardsDataAdapter.GetEmailSummaryAssignmentChanges();
                        
                        // Filter changes for the specific check group if specified
                        var relevantChanges = emailSummary.Changes;
                        if (!string.IsNullOrEmpty(template.TemplateCheckGroup))
                        {
                            relevantChanges = emailSummary.Changes
                                .Where(c => c.GroupName.Equals(template.TemplateCheckGroup, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                        }

                        if (relevantChanges.Any())
                        {
                            logger.Information($"Found {relevantChanges.Count} relevant changes for template {template.TemplateId}");
                            
                            // Process each relevant change with the template
                            foreach (var change in relevantChanges)
                            {
                                await dataOrchestrator.ProcessTemplateWithAssignmentData(template.TemplateId, change);
                            }
                        }
                        else
                        {
                            logger.Information($"No relevant changes found for template {template.TemplateId} in group {template.TemplateCheckGroup}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error processing alarm-triggered template: {template.TemplateId}");
                }
            }
        }
    }

    public class AlarmConfiguration
    {
        public string AlarmName { get; set; } = string.Empty;
        public TimeSpan AlarmTime { get; set; }
    }

    public class TemplateConfiguration
    {
        public string TemplateId { get; set; } = string.Empty;
        public string TemplateAlarm { get; set; } = string.Empty;
        public string TemplateCheckGroup { get; set; } = string.Empty;
    }
}
