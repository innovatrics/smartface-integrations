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
    /// <summary>
    /// Background service that monitors locker access events and automatically releases lockers
    /// when an authorized user picks up their item, then sends a confirmation email.
    /// </summary>
    public class LockerPickupService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IDataOrchestrator dataOrchestrator;
        private readonly IDashboardsDataAdapter dashboardsDataAdapter;
        private readonly ISmtpMailAdapter smtpMailAdapter;
        
        // Configuration values for the "Courier Item Picked Up" template
        private readonly bool isEnabled;
        private readonly string templateId = string.Empty;
        private readonly string templateName = string.Empty;
        private readonly List<string> templateCheckGroups = new List<string>();
        private readonly int timeToCheckForAccessEventsMs;
        private readonly int refreshPeriodMs;
        
        // Track processed events to avoid duplicate processing
        private readonly HashSet<long> processedEventIds = new HashSet<long>();
        private readonly object lockObject = new object();

        public LockerPickupService(
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

            // Load configuration for "Courier Item Picked Up" template
            var templatesSection = configuration.GetSection("LockerMailer:Templates");
            foreach (var template in templatesSection.GetChildren())
            {
                var configTemplateName = template.GetValue<string>("templateName");
                if (!string.IsNullOrEmpty(configTemplateName) && 
                    configTemplateName.Equals("Courier Item Picked Up", StringComparison.OrdinalIgnoreCase))
                {
                    templateId = template.GetValue<string>("templateId") ?? "lockers-flow_9";
                    templateName = configTemplateName;
                    
                    // Get check groups - can be a single string or an array
                    var singleGroup = template.GetValue<string>("templateCheckGroup");
                    if (!string.IsNullOrEmpty(singleGroup))
                    {
                        templateCheckGroups = new List<string> { singleGroup };
                    }
                    else
                    {
                        templateCheckGroups = template.GetSection("templateCheckGroup").Get<List<string>>() ?? new List<string>();
                    }
                    
                    timeToCheckForAccessEventsMs = template.GetValue<int>("timeToCheckForAccessEventsMs", 300000); // Default 5 minutes
                    refreshPeriodMs = template.GetValue<int>("refreshPeriodMs", 300000); // Default 5 minutes
                    isEnabled = true;
                    
                    logger.Information($"[LockerPickupService] Loaded configuration - Template: {templateId}, CheckGroups: [{string.Join(", ", templateCheckGroups)}], TimeToCheckForAccessEvents: {timeToCheckForAccessEventsMs}ms, RefreshPeriod: {refreshPeriodMs}ms");
                    break;
                }
            }

            if (!isEnabled)
            {
                logger.Information("[LockerPickupService] 'Courier Item Picked Up' template not found in configuration - service will be disabled");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (!isEnabled)
            {
                logger.Information("[LockerPickupService] Service is disabled - no 'Courier Item Picked Up' template configured");
                return;
            }

            logger.Information($"[LockerPickupService] Started - checking every {refreshPeriodMs}ms for locker pickups");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndProcessPickedUpLockers();
                }
                catch (OperationCanceledException)
                {
                    logger.Information("[LockerPickupService] Service is shutting down");
                    break;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "[LockerPickupService] Error during locker pickup check");
                }

                // Wait for the configured refresh period
                await Task.Delay(refreshPeriodMs, cancellationToken);
            }
        }

        private async Task CheckAndProcessPickedUpLockers()
        {
            logger.Debug("[LockerPickupService] Starting locker pickup check...");

            // Calculate the time window: current time - timeToCheckForAccessEventsMs
            var fromDateTime = DateTime.UtcNow.AddMilliseconds(-timeToCheckForAccessEventsMs);
            logger.Debug($"[LockerPickupService] Checking for locker access events since {fromDateTime:yyyy-MM-ddTHH:mm:ssZ}");

            // Step 1: Get accessed lockers from the Dashboards API
            var accessEvents = await dashboardsDataAdapter.GetAccessedLockersAsync(fromDateTime);
            
            if (accessEvents == null || !accessEvents.Any())
            {
                logger.Debug("[LockerPickupService] No locker access events found in the time window");
                return;
            }

            logger.Information($"[LockerPickupService] Found {accessEvents.Count} locker access events to process");

            // Step 2: Filter events where carrierId is not null (authorized access)
            var authorizedEvents = accessEvents.Where(e => e.CarrierId.HasValue).ToList();
            
            if (!authorizedEvents.Any())
            {
                logger.Debug("[LockerPickupService] No authorized locker access events (with carrierId) found");
                return;
            }

            logger.Information($"[LockerPickupService] Found {authorizedEvents.Count} authorized access events (with carrierId)");

            // Step 3: Get groups to find locker details
            var groups = await dashboardsDataAdapter.GetGroups();
            if (groups == null || !groups.Any())
            {
                logger.Warning("[LockerPickupService] No groups data available - cannot process locker pickups");
                return;
            }

            // Step 4: Process each authorized event
            foreach (var accessEvent in authorizedEvents)
            {
                // Skip if already processed
                lock (lockObject)
                {
                    if (processedEventIds.Contains(accessEvent.Id))
                    {
                        logger.Debug($"[LockerPickupService] Event {accessEvent.Id} already processed - skipping");
                        continue;
                    }
                }

                try
                {
                    await ProcessLockerPickupEvent(accessEvent, groups);
                    
                    // Mark as processed
                    lock (lockObject)
                    {
                        processedEventIds.Add(accessEvent.Id);
                        
                        // Clean up old event IDs to prevent memory growth (keep last 1000)
                        if (processedEventIds.Count > 1000)
                        {
                            var oldestIds = processedEventIds.OrderBy(id => id).Take(500).ToList();
                            foreach (var id in oldestIds)
                            {
                                processedEventIds.Remove(id);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"[LockerPickupService] Error processing access event {accessEvent.Id}");
                }
            }
        }

        private async Task ProcessLockerPickupEvent(LockerAccessEvent accessEvent, List<GroupInfo> allGroups)
        {
            var lockerName = accessEvent.AccesspointName;
            
            if (string.IsNullOrEmpty(lockerName))
            {
                logger.Warning($"[LockerPickupService] Event {accessEvent.Id} has no AccesspointName - skipping");
                return;
            }

            logger.Debug($"[LockerPickupService] Processing event {accessEvent.Id} - Locker: {lockerName}, Carrier: {accessEvent.CarrierFullName} (ID: {accessEvent.CarrierId})");

            // Find the locker in the groups and check if it belongs to a configured check group
            LockerInfo? foundLocker = null;
            GroupInfo? foundGroup = null;

            foreach (var group in allGroups)
            {
                // Check if this group is in our templateCheckGroups
                if (!templateCheckGroups.Any(tcg => tcg.Equals(group.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Find the locker by name in this group
                var locker = group.AllLockers?.FirstOrDefault(l => 
                    l.Name.Equals(lockerName, StringComparison.OrdinalIgnoreCase));
                
                if (locker != null)
                {
                    foundLocker = locker;
                    foundGroup = group;
                    break;
                }
            }

            if (foundLocker == null || foundGroup == null)
            {
                logger.Debug($"[LockerPickupService] Locker '{lockerName}' not found in configured check groups [{string.Join(", ", templateCheckGroups)}] - skipping");
                return;
            }

            logger.Information($"[LockerPickupService] Locker '{lockerName}' (ID: {foundLocker.Id}) found in group '{foundGroup.Name}' - processing pickup");

            // Step 1: Release the locker
            logger.Information($"[LockerPickupService] Releasing locker '{lockerName}' (ID: {foundLocker.Id})");
            var releaseResult = await dashboardsDataAdapter.ReleaseLockerAsync(foundLocker.Id);
            
            if (!releaseResult.Success)
            {
                logger.Error($"[LockerPickupService] Failed to release locker '{lockerName}' (ID: {foundLocker.Id}): {releaseResult.Message}");
                // Continue to send email anyway to notify the user
            }
            else
            {
                logger.Information($"[LockerPickupService] Successfully released locker '{lockerName}' (ID: {foundLocker.Id})");
            }

            // Step 2: Send email using lockers-flow_9 template
            await SendPickupConfirmationEmail(accessEvent, foundLocker, foundGroup, allGroups);
        }

        private async Task SendPickupConfirmationEmail(LockerAccessEvent accessEvent, LockerInfo locker, GroupInfo group, List<GroupInfo> allGroups)
        {
            try
            {
                // Get the employee email - first try from locker assignment, then search by CarrierId
                var employeeEmail = locker.AssignedEmployeeEmail;
                var employeeName = accessEvent.CarrierFullName ?? locker.AssignedEmployeeName ?? "Unknown";

                // If locker doesn't have email (might be already unassigned), try to find by CarrierId
                if (string.IsNullOrEmpty(employeeEmail) && accessEvent.CarrierId.HasValue)
                {
                    logger.Debug($"[LockerPickupService] Locker doesn't have email, searching by CarrierId {accessEvent.CarrierId}");
                    
                    // Search all groups for any locker assigned to this carrier to get their email
                    foreach (var g in allGroups)
                    {
                        var lockerWithEmail = g.AllLockers?.FirstOrDefault(l => 
                            l.AssignedTo == (int)accessEvent.CarrierId.Value && 
                            !string.IsNullOrEmpty(l.AssignedEmployeeEmail));
                        
                        if (lockerWithEmail != null)
                        {
                            employeeEmail = lockerWithEmail.AssignedEmployeeEmail;
                            if (string.IsNullOrEmpty(employeeName) || employeeName == "Unknown")
                            {
                                employeeName = lockerWithEmail.AssignedEmployeeName;
                            }
                            logger.Debug($"[LockerPickupService] Found email '{employeeEmail}' for carrier {accessEvent.CarrierId} from locker '{lockerWithEmail.Name}'");
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(employeeEmail))
                {
                    logger.Warning($"[LockerPickupService] No email address found for employee '{employeeName}' (CarrierId: {accessEvent.CarrierId}) - cannot send pickup confirmation");
                    return;
                }

                logger.Information($"[LockerPickupService] Sending pickup confirmation email to {employeeName} ({employeeEmail}) for locker '{locker.Name}'");

                // Create AssignmentChange for template processing
                var mockChange = new AssignmentChange
                {
                    LockerId = locker.Id,
                    LockerName = locker.Name,
                    GroupName = group.Name,
                    NewAssignedTo = locker.AssignedTo,
                    NewAssignedEmployeeName = employeeName,
                    NewAssignedEmployeeEmail = employeeEmail,
                    NewAssignedEmployeeIdentifier = locker.AssignedEmployeeIdentifier,
                    ChangeTimestamp = accessEvent.DateTime,
                    ChangeType = "PickedUp",
                    AllAssignedLockerNames = new List<string> { locker.Name },
                    TotalAssignedLockers = 1
                };

                // Get Keila campaigns through the data orchestrator
                List<KeilaCampaign> keilaCampaigns = new List<KeilaCampaign>();
                try
                {
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
                                logger.Debug($"[LockerPickupService] Retrieved {keilaCampaigns.Count} Keila campaigns");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "[LockerPickupService] Failed to get Keila campaigns");
                }

                // Process the template
                await dataOrchestrator.ProcessTemplateWithAssignmentData(templateId, mockChange, keilaCampaigns);
                
                logger.Information($"[LockerPickupService] Successfully sent pickup confirmation email to {employeeEmail}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"[LockerPickupService] Failed to send pickup confirmation email for locker '{locker.Name}'");
            }
        }
    }
}

