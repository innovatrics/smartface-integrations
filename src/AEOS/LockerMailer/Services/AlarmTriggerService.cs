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
        private readonly ISmtpMailAdapter smtpMailAdapter;
        private readonly Dictionary<string, DateTime> lastTriggeredTimes = new Dictionary<string, DateTime>();
        private readonly List<AlarmConfiguration> alarmConfigurations = new List<AlarmConfiguration>();
        private readonly List<TemplateConfiguration> templateConfigurations = new List<TemplateConfiguration>();

        public AlarmTriggerService(
            ILogger logger,
            IConfiguration configuration,
            IDataOrchestrator dataOrchestrator,
            IDashboardsDataAdapter dashboardsDataAdapter,
            ISmtpMailAdapter smtpMailAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.dataOrchestrator = dataOrchestrator ?? throw new ArgumentNullException(nameof(dataOrchestrator));
            this.dashboardsDataAdapter = dashboardsDataAdapter ?? throw new ArgumentNullException(nameof(dashboardsDataAdapter));
            this.smtpMailAdapter = smtpMailAdapter ?? throw new ArgumentNullException(nameof(smtpMailAdapter));

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
                    var autoReleaseLockers = template.GetValue<bool>("autoReleaseLockers", false);
                    
                    if (!string.IsNullOrEmpty(templateId) && !string.IsNullOrEmpty(templateAlarm))
                    {
                        templateConfigurations.Add(new TemplateConfiguration
                        {
                            TemplateId = templateId,
                            TemplateAlarm = templateAlarm,
                            TemplateCheckGroup = templateCheckGroup ?? string.Empty,
                            AutoReleaseLockers = autoReleaseLockers
                        });
                        logger.Debug($"Loaded template configuration: {templateId} with alarm {templateAlarm}, autoReleaseLockers: {autoReleaseLockers}");
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
                        // Check if lockers-flow_3, lockers-flow_4, lockers-flow_5, or lockers-flow_6 is among the triggered templates
                        var lockersFlow3Triggered = templatesToTrigger.Any(t => t.TemplateId.Equals("lockers-flow_3", StringComparison.OrdinalIgnoreCase));
                        var lockersFlow4Triggered = templatesToTrigger.Any(t => t.TemplateId.Equals("lockers-flow_4", StringComparison.OrdinalIgnoreCase));
                        var lockersFlow5Triggered = templatesToTrigger.Any(t => t.TemplateId.Equals("lockers-flow_5", StringComparison.OrdinalIgnoreCase));
                        var lockersFlow6Triggered = templatesToTrigger.Any(t => t.TemplateId.Equals("lockers-flow_6", StringComparison.OrdinalIgnoreCase));
                        if (lockersFlow3Triggered)
                        {
                            logger.Information("lockers-flow_3 triggered as it is time");
                        }
                        if (lockersFlow4Triggered)
                        {
                            logger.Information("lockers-flow_4 triggered as it is time");
                        }
                        if (lockersFlow5Triggered)
                        {
                            logger.Information("lockers-flow_5 triggered as it is time");
                        }
                        if (lockersFlow6Triggered)
                        {
                            logger.Information("lockers-flow_6 triggered as it is time");
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
                        var taskResult = getCampaignsMethod.Invoke(keilaDataAdapter, null);
                        if (taskResult is Task<List<KeilaCampaign>> task)
                        {
                            keilaCampaigns = await task;
                            logger.Debug($"Successfully retrieved {keilaCampaigns.Count} Keila campaigns for all template processing");
                        }
                        else
                        {
                            logger.Warning("GetCampaignsWithTemplatesAsync method did not return expected Task<List<KeilaCampaign>>");
                        }
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

                    // Special handling for lockers-flow_5 (bulk email to receptionist)
                    if (template.TemplateId.Equals("lockers-flow_5", StringComparison.OrdinalIgnoreCase))
                    {
                        await ProcessLockersFlow5BulkEmail(template, groups, keilaCampaigns);
                    }
                    // Special handling for lockers-flow_3, lockers-flow_4, and lockers-flow_6 to show detailed assignment information
                    else if (template.TemplateId.Equals("lockers-flow_3", StringComparison.OrdinalIgnoreCase) || 
                        template.TemplateId.Equals("lockers-flow_4", StringComparison.OrdinalIgnoreCase) ||
                        template.TemplateId.Equals("lockers-flow_6", StringComparison.OrdinalIgnoreCase))
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
                                    var employeeGroups = assignedLockers.GroupBy(l => l.AssignedTo!.Value).ToList();
                                    
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

        private async Task ProcessLockersFlow5BulkEmail(TemplateConfiguration template, List<GroupInfo> groups, List<KeilaCampaign> keilaCampaigns)
        {
            try
            {
                logger.Information($"Processing lockers-flow_5 bulk email for receptionist");
                
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
                            logger.Information($"Found {assignedLockers.Count} assigned lockers for bulk email processing");
                            
                            // Create a single bulk email with all locker information
                            await SendBulkEmailToReceptionist(template, assignedLockers, keilaCampaigns);
                            
                            // Release lockers if autoReleaseLockers is enabled
                            if (template.AutoReleaseLockers)
                            {
                                logger.Information($"[Locker Release] Auto-release enabled for lockers-flow_5. Starting release of {assignedLockers.Count} lockers...");
                                await ReleaseLockers(assignedLockers);
                            }
                            else
                            {
                                logger.Information($"[Locker Release] Auto-release is disabled for lockers-flow_5. Skipping locker release.");
                            }
                        }
                        else
                        {
                            logger.Information($"No assigned lockers found in '{template.TemplateCheckGroup}' group - no email to send");
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
            catch (Exception ex)
            {
                logger.Error(ex, $"Error processing lockers-flow_5 bulk email");
            }
        }

        private async Task ReleaseLockers(List<LockerInfo> lockers)
        {
            var successCount = 0;
            var failureCount = 0;
            var results = new List<LockerReleaseResult>();

            logger.Information($"[Locker Release] Beginning release of {lockers.Count} lockers...");

            foreach (var locker in lockers)
            {
                try
                {
                    logger.Information($"[Locker Release] Attempting to release locker '{locker.Name}' (ID: {locker.Id}) assigned to '{locker.AssignedEmployeeName}'");
                    
                    var result = await dashboardsDataAdapter.ReleaseLockerAsync(locker.Id);
                    results.Add(result);

                    if (result.Success)
                    {
                        successCount++;
                        logger.Information($"[Locker Release] Successfully released locker '{locker.Name}' (ID: {locker.Id})");
                    }
                    else
                    {
                        failureCount++;
                        logger.Warning($"[Locker Release] Failed to release locker '{locker.Name}' (ID: {locker.Id}) - Status: {result.StatusCode}, Message: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    logger.Error(ex, $"[Locker Release] Exception while releasing locker '{locker.Name}' (ID: {locker.Id})");
                }
            }

            // Summary log
            logger.Information($"[Locker Release] Release operation completed. Total: {lockers.Count}, Successful: {successCount}, Failed: {failureCount}");
            
            if (failureCount > 0)
            {
                logger.Warning($"[Locker Release] {failureCount} locker(s) could not be released. Check previous logs for details.");
            }
        }

        private async Task SendBulkEmailToReceptionist(TemplateConfiguration template, List<LockerInfo> assignedLockers, List<KeilaCampaign> keilaCampaigns)
        {
            try
            {
                // Get receptionist emails from configuration
                var receptionistEmails = configuration.GetSection("LockerMailer:ReceptionistEmails").Get<string[]>() ?? new string[0];
                
                if (!receptionistEmails.Any())
                {
                    logger.Warning("No receptionist emails configured - cannot send bulk email");
                    return;
                }

                // Find the Keila campaign for this template
                var campaign = keilaCampaigns.FirstOrDefault(c =>
                    c.Id.Equals(template.TemplateId, StringComparison.OrdinalIgnoreCase) ||
                    c.Subject.Equals(template.TemplateId, StringComparison.OrdinalIgnoreCase));

                if (campaign == null)
                {
                    logger.Warning($"Template '{template.TemplateId}' not found among Keila campaigns");
                    return;
                }

                // Get template name from configuration as fallback
                string templateName = string.Empty;
                try
                {
                    var templatesSection = configuration.GetSection("LockerMailer:Templates");
                    foreach (var configTemplate in templatesSection.GetChildren())
                    {
                        var configTemplateId = configTemplate.GetValue<string>("templateId");
                        if (!string.IsNullOrEmpty(configTemplateId) && configTemplateId.Equals(template.TemplateId, StringComparison.OrdinalIgnoreCase))
                        {
                            templateName = configTemplate.GetValue<string>("templateName") ?? string.Empty;
                            logger.Debug($"Loaded templateName '{templateName}' for templateId '{template.TemplateId}'");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to load templateName for templateId '{template.TemplateId}'");
                }

                // Create a bulleted list of all lockers for the placeholder replacement
                var lockerList = string.Join("<br/>", assignedLockers.Select(locker => $"• {locker.Name} (Employee: {locker.AssignedEmployeeName})"));

                // Create a mock AssignmentChange for template processing
                var mockChange = new AssignmentChange
                {
                    LockerId = assignedLockers.FirstOrDefault()?.Id ?? 0,
                    LockerName = "Multiple Lockers", // Generic name since this is a bulk email
                    GroupName = template.TemplateCheckGroup,
                    NewAssignedTo = null, // Not applicable for bulk email
                    NewAssignedEmployeeName = "Multiple Employees", // Generic name
                    NewAssignedEmployeeEmail = string.Join(", ", receptionistEmails), // Receptionist emails
                    NewAssignedEmployeeIdentifier = string.Empty,
                    ChangeTimestamp = DateTime.UtcNow,
                    ChangeType = "BulkRelease",
                    // Custom properties for lockers-flow_5 template
                    AllAssignedLockerNames = assignedLockers.Select(l => l.Name).ToList(),
                    TotalAssignedLockers = assignedLockers.Count
                };

                // Get variable dump for logging (reuse the same logic as ReplaceTemplatePlaceholdersForBulk)
                var templateCancelTime = LoadTemplateCancelTime(template.TemplateId);
                var variableDump = new Dictionary<string, string?>
                {
                    { "fullname", mockChange.NewAssignedEmployeeName },
                    { "prev_fullname", mockChange.PreviousAssignedEmployeeName },
                    { "time", DateTime.Now.ToString("HH:mm") },
                    { "source", mockChange.LockerName },
                    { "group", mockChange.GroupName },
                    { "canceltime", templateCancelTime },
                    { "lockercount", mockChange.TotalAssignedLockers.ToString() },
                    { "lockerlist", lockerList }
                };

                // Build HTML from Keila template blocks with placeholder replacement
                var sb = new System.Text.StringBuilder();
                sb.Append("<html><body>");

                if (campaign.JsonBody?.Blocks != null)
                {
                    foreach (var block in campaign.JsonBody.Blocks)
                    {
                        var rawText = block.Data?.Text ?? string.Empty;
                        var processedText = ReplaceTemplatePlaceholdersForBulk(rawText, mockChange, template.TemplateId, lockerList);
                        sb.Append("<p>").Append(processedText).Append("</p>");
                    }
                }

                sb.Append("</body></html>");
                var htmlEmail = sb.ToString();

                // Log the generated HTML for debugging
                logger.Information("Generated bulk HTML email for receptionist using Keila template:\n" + htmlEmail);

                // Send email to all receptionist addresses
                var debugMode = configuration.GetValue<bool>("LockerMailer:DebugMode", false);
                if (!debugMode)
                {
                    var subject = !string.IsNullOrEmpty(templateName) ? templateName : (campaign.Subject ?? template.TemplateId);
                    
                    foreach (var receptionistEmail in receptionistEmails)
                    {
                        if (!string.IsNullOrWhiteSpace(receptionistEmail))
                        {
                            // Create logging data for bulk email
                            var loggingData = new MailLoggingData
                            {
                                TemplateUsed = template.TemplateId,
                                EmployeeName = "Receptionist (Bulk Email)",
                                EmployeeId = null,
                                VariableDump = variableDump
                            };

                            await smtpMailAdapter.SendAsync(receptionistEmail, subject, htmlEmail, loggingData);
                            logger.Information($"Bulk email sent to receptionist: {receptionistEmail} with subject '{subject}'");
                        }
                    }
                }
                else
                {
                    logger.Information("Debug mode enabled - bulk email not sent to receptionist");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error sending bulk email to receptionist");
            }
        }

        private string ReplaceTemplatePlaceholdersForBulk(string rawText, AssignmentChange change, string templateId, string lockerList)
        {
            if (string.IsNullOrEmpty(rawText)) return string.Empty;

            // Load template-specific cancelTime
            var templateCancelTime = LoadTemplateCancelTime(templateId);

            var replacements = new Dictionary<string, string?>
            {
                { "fullname", change.NewAssignedEmployeeName },
                { "prev_fullname", change.PreviousAssignedEmployeeName },
                { "time", DateTime.Now.ToString("HH:mm") },
                { "source", change.LockerName },
                { "group", change.GroupName },
                { "canceltime", templateCancelTime },
                { "lockercount", change.TotalAssignedLockers.ToString() },
                { "lockerlist", lockerList } // This is the key replacement for the bulk email
            };

            string processed = rawText;
            foreach (var kv in replacements)
            {
                var val = kv.Value ?? string.Empty;
                // Common variants: with double braces, with single braces, with/without spaces
                var tokens = new[]
                {
                    $"{{{{ campaign.data.{kv.Key} }}}}",
                    $"{{{{campaign.data.{kv.Key} }}",
                    $"{{ campaign.data.{kv.Key} }}",
                    $"{{ campaign.data.{kv.Key} }}",
                    $"{{ campaign.data.{kv.Key} }}",
                    $"{{ campaign.data.{kv.Key} }}",
                    $"{{ campaign.data.{kv.Key} }}",
                    $"{{ campaign.data.{kv.Key} }}"
                };
                foreach (var t in tokens)
                {
                    processed = processed.Replace(t, val);
                }
            }
            return processed;
        }

        private string LoadTemplateCancelTime(string templateId)
        {
            try
            {
                var templatesSection = configuration.GetSection("LockerMailer:Templates");
                foreach (var template in templatesSection.GetChildren())
                {
                    var configTemplateId = template.GetValue<string>("templateId");
                    if (!string.IsNullOrEmpty(configTemplateId) && configTemplateId.Equals(templateId, StringComparison.OrdinalIgnoreCase))
                    {
                        var configuredCancelTime = template.GetValue<string>("cancelTime");
                        if (!string.IsNullOrEmpty(configuredCancelTime))
                        {
                            return configuredCancelTime;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to load cancelTime for template '{templateId}', using default: 18:00");
            }
            
            return "18:00"; // Fallback to default
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
        public bool AutoReleaseLockers { get; set; } = false;
    }
}
