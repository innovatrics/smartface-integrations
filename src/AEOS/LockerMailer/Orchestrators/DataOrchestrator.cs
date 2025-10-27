using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using Innovatrics.SmartFace.Integrations.LockerMailer;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;
using System.IO;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class DataOrchestrator : IDataOrchestrator
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IKeilaDataAdapter keilaDataAdapter;
        private readonly ISmtpMailAdapter smtpMailAdapter;
        private readonly IDashboardsDataAdapter dashboardsDataAdapter;
        private readonly string lockersFlow1CheckGroup = string.Empty;
        private readonly string lockersFlow2CheckGroup = string.Empty;
        private string cancelTime = "18:00"; // Default value
        
        // Helper map for placeholder keys
        private string ReplaceTemplatePlaceholders(string rawText, AssignmentChange change, string templateId = "")
        {
            if (string.IsNullOrEmpty(rawText)) return string.Empty;

            // Load template-specific cancelTime
            var templateCancelTime = LoadTemplateCancelTime(templateId);

            // Create bulleted list of locker names (for lockers-flow_3)
            var lockerList = string.Join("<br/>", change.AllAssignedLockerNames.Select(name => $"• {name}"));

            var replacements = new Dictionary<string, string?>
            {
                { "fullname", change.NewAssignedEmployeeName },
                { "prev_fullname", change.PreviousAssignedEmployeeName },
                { "time", DateTime.Now.ToString("HH:mm") },
                { "source", change.LockerName },
                { "group", change.GroupName },
                { "canceltime", templateCancelTime },
                { "lockercount", change.TotalAssignedLockers.ToString() },
                { "lockerlist", lockerList }
            };

            string processed = rawText;
            foreach (var kv in replacements)
            {
                var val = kv.Value ?? string.Empty;
                // common variants: with double braces, with single braces, with/without spaces
                var tokens = new []
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
                logger.Error(ex, $"Failed to load cancelTime for template '{templateId}', using default: {this.cancelTime}");
            }
            
            return this.cancelTime; // Fallback to default
        }

        // Fields for tracking assignment changes (commented out - not currently used)
        //private Dictionary<long, long?> _previousLockerAssignments = new Dictionary<long, long?>();
        //private DateTime? _lastAssignmentCheckTime = null;
        //private List<LockerAssignmentChange> _assignmentChanges = new List<LockerAssignmentChange>();

        public DataOrchestrator(
            ILogger logger,
            IConfiguration configuration,
            IKeilaDataAdapter keilaDataAdapter,
            ISmtpMailAdapter smtpMailAdapter,
            IDashboardsDataAdapter dashboardsDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.keilaDataAdapter = keilaDataAdapter ?? throw new ArgumentNullException(nameof(keilaDataAdapter));
            this.smtpMailAdapter = smtpMailAdapter ?? throw new ArgumentNullException(nameof(smtpMailAdapter));
            this.dashboardsDataAdapter = dashboardsDataAdapter ?? throw new ArgumentNullException(nameof(dashboardsDataAdapter));

            // Load lockers-flow_1 templateCheckGroup from configuration
            try
            {
                var templatesSection = configuration.GetSection("LockerMailer:Templates");
                foreach (var template in templatesSection.GetChildren())
                {
                    var templateId = template.GetValue<string>("templateId");
                    if (!string.IsNullOrEmpty(templateId) && templateId.Equals("lockers-flow_1", StringComparison.OrdinalIgnoreCase))
                    {
                        this.lockersFlow1CheckGroup = template.GetValue<string>("templateCheckGroup", string.Empty);
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(this.lockersFlow1CheckGroup))
                {
                    this.logger.Debug($"Loaded lockers-flow_1 check group from configuration: '{this.lockersFlow1CheckGroup}'");
                }
                else
                {
                    this.logger.Warning("No templateCheckGroup configured for lockers-flow_1; trigger will be skipped.");
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to load lockers-flow_1 templateCheckGroup from configuration");
            }
            try
            {
                var templatesSection = configuration.GetSection("LockerMailer:Templates");
                foreach (var template in templatesSection.GetChildren())
                {
                    var templateId = template.GetValue<string>("templateId");
                    if (!string.IsNullOrEmpty(templateId) && templateId.Equals("lockers-flow_2", StringComparison.OrdinalIgnoreCase))
                    {
                        this.lockersFlow2CheckGroup = template.GetValue<string>("templateCheckGroup", string.Empty);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to load lockers-flow_2 templateCheckGroup from configuration");
            }

            // Load cancelTime from lockers-flow_1 template configuration
            try
            {
                var templatesSection = configuration.GetSection("LockerMailer:Templates");
                foreach (var template in templatesSection.GetChildren())
                {
                    var templateId = template.GetValue<string>("templateId");
                    if (!string.IsNullOrEmpty(templateId) && templateId.Equals("lockers-flow_1", StringComparison.OrdinalIgnoreCase))
                    {
                        var configuredCancelTime = template.GetValue<string>("cancelTime");
                        if (!string.IsNullOrEmpty(configuredCancelTime))
                        {
                            this.cancelTime = configuredCancelTime;
                            this.logger.Debug($"Loaded cancelTime from configuration: '{this.cancelTime}'");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to load cancelTime from configuration, using default: 18:00");
            }
        }

        public async Task ProcessEmailSummaryAssignmentChanges(EmailSummaryResponse emailSummary)
        {
            try
            {
                // Skip processing if there are no changes
                if (emailSummary.TotalChanges == 0)
                {
                    logger.Debug("No assignment changes to process - skipping");
                    return;
                }

                logger.Information($"Processing {emailSummary.TotalChanges} assignment changes");

                // Get cached Keila campaigns for template processing
                var keilaCampaigns = await keilaDataAdapter.GetCampaignsWithTemplatesAsync();
                logger.Information($"Retrieved {keilaCampaigns.Count} Keila campaigns for template processing");

                // Process each assignment change
                foreach (var change in emailSummary.Changes)
                {
                    var prevName = string.IsNullOrWhiteSpace(change.PreviousAssignedEmployeeName) ? "NULL" : change.PreviousAssignedEmployeeName;
                    var newName = string.IsNullOrWhiteSpace(change.NewAssignedEmployeeName) ? "NULL" : change.NewAssignedEmployeeName;
                    logger.Information($"Processing change: Locker {change.LockerName} assigned from {prevName} to {newName}");

                    // Determine which template to use based on the change type or locker function
                    var templateToUse = DetermineTemplateForChange(change);
                    
                    if (templateToUse != null)
                    {
                        logger.Information($"Using template: {templateToUse}");
                        
                        // Process the template with the assignment data
                        await ProcessTemplateWithAssignmentData(templateToUse, change);
                    }
                    else
                    {
                        logger.Warning($"No suitable template found for change type: {change.ChangeType}");
                    }

                    logger.Information($"Change processed: {change.ChangeType} at {change.ChangeTimestamp}");
                }

                await Task.CompletedTask; // Placeholder for async operations
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing email summary assignment changes");
                throw;
            }
        }

        private string? DetermineTemplateForChange(AssignmentChange change)
        {

            var chosenFlowTemplate = string.Empty;
            // Check for specific triggers to trigger email templates:
            
        // 1# lockers-flow_1
            // If the information says that you have been newly assigned a locker
            // from the locker group FOOD, trigger this event
            // (for now: just log that it was triggered).
            if (
                change != null &&
                change.ChangeType.Equals("Assigned", StringComparison.OrdinalIgnoreCase) &&
                change.NewAssignedTo.HasValue &&
                !string.IsNullOrEmpty(this.lockersFlow1CheckGroup) &&
                string.Equals(change.GroupName, this.lockersFlow1CheckGroup, StringComparison.OrdinalIgnoreCase)
            )
            {
                chosenFlowTemplate = "lockers-flow_1";

                logger.Information(
                    $"[Trigger] lockers-flow_1 matched for locker '{change.LockerName}' in group '{change.GroupName}' " +
                    $"for employee '{change.NewAssignedEmployeeName}' (ID: {change.NewAssignedTo}) at {change.ChangeTimestamp}. {chosenFlowTemplate}"
                );

                return chosenFlowTemplate;
            }

        // 2# lockers-flow_2
            // If the information says that you have been newly assigned a locker
            // from the locker group COURIER, trigger this event
            // (for now: just log that it was triggered).
            else if (
                change != null &&
                change.ChangeType.Equals("Assigned", StringComparison.OrdinalIgnoreCase) &&
                change.NewAssignedTo.HasValue &&
                !string.IsNullOrEmpty(this.lockersFlow2CheckGroup) &&
                string.Equals(change.GroupName, this.lockersFlow2CheckGroup, StringComparison.OrdinalIgnoreCase)
            )
            {
                chosenFlowTemplate = "lockers-flow_2";

                logger.Information(
                    $"[Trigger] lockers-flow_2 matched for locker '{change.LockerName}' in group '{change.GroupName}' " +
                    $"for employee '{change.NewAssignedEmployeeName}' (ID: {change.NewAssignedTo}) at {change.ChangeTimestamp}. {chosenFlowTemplate}"
                );

                return chosenFlowTemplate;
            }

            // Note: lockers-flow_3, lockers-flow_4, and lockers-flow_5 are handled by AlarmTriggerService based on time triggers
            // and do not need to be processed here as they are triggered by alarms, not assignment changes

            else
            {
                return null;
            }
            // 3# lockers-flow_3 (handled by AlarmTriggerService)
            // 4# lockers-flow_4 (handled by AlarmTriggerService)
            // 5# lockers-flow_5 (handled by AlarmTriggerService)
            // 6#
            // 7#
            // 8#
            // 9#

        }

        public async Task ProcessTemplateWithAssignmentData(string templateId, AssignmentChange change)
        {
            try
            {
                logger.Information($"Processing template '{templateId}' for assignment change");

                // Load templateName for this specific templateId from configuration
                string templateName = string.Empty;
                try
                {
                    var templatesSection = configuration.GetSection("LockerMailer:Templates");
                    foreach (var template in templatesSection.GetChildren())
                    {
                        var configTemplateId = template.GetValue<string>("templateId");
                        if (!string.IsNullOrEmpty(configTemplateId) && configTemplateId.Equals(templateId, StringComparison.OrdinalIgnoreCase))
                        {
                            templateName = template.GetValue<string>("templateName") ?? string.Empty;
                            logger.Information($"Loaded templateName '{templateName}' for templateId '{templateId}'");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to load templateName for templateId '{templateId}'");
                }

                // Load campaigns to find the matching template content
                var campaigns = await keilaDataAdapter.GetCampaignsWithTemplatesAsync();
                var campaign = campaigns.FirstOrDefault(c =>
                    c.Id.Equals(templateId, StringComparison.OrdinalIgnoreCase) ||
                    c.Subject.Equals(templateId, StringComparison.OrdinalIgnoreCase));

                if (campaign == null)
                {
                    logger.Warning($"Template '{templateId}' not found among Keila campaigns");
                    return;
                }

                // Build HTML from blocks (each block's data.text becomes a paragraph)
                var sb = new System.Text.StringBuilder();
                sb.Append("<html><body>");

                if (campaign.JsonBody?.Blocks != null)
                {
                    foreach (var block in campaign.JsonBody.Blocks)
                    {
                        var rawText = block.Data?.Text ?? string.Empty;
                        var processedText = ReplaceTemplatePlaceholders(rawText, change, templateId);

                        sb.Append("<p>").Append(processedText).Append("</p>");
                    }
                }

                sb.Append("</body></html>");
                var htmlEmail = sb.ToString();

                // For now, print the HTML to the terminal for debugging
                logger.Information("Generated HTML email:\n" + htmlEmail);

                // In DebugMode, only log the HTML; otherwise send
                var debugMode = configuration.GetValue<bool>("LockerMailer:DebugMode", false);
                if (!debugMode)
                {
                    // Determine recipient and subject based on change type
                    var recipientEmail = change.ChangeType.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)
                        ? change.PreviousAssignedEmployeeEmail
                        : change.NewAssignedEmployeeEmail;

                    if (!string.IsNullOrWhiteSpace(recipientEmail))
                    {
                        var subject = !string.IsNullOrEmpty(templateName) ? templateName : (campaign.Subject ?? templateId);
                        await smtpMailAdapter.SendAsync(recipientEmail, subject, htmlEmail);
                        logger.Information($"Email sent to {recipientEmail} with subject '{subject}' for locker '{change.LockerName}' change {change.ChangeType}");
                    }
                    else
                    {
                        logger.Warning($"No recipient email available for locker '{change.LockerName}' change {change.ChangeType}");
                    }
                }

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error processing template '{templateId}' with assignment data");
                throw;
            }
        }

        public async Task ProcessTemplateWithAssignmentData(string templateId, AssignmentChange change, List<KeilaCampaign> preFetchedCampaigns)
        {
            try
            {
                logger.Debug($"Processing template '{templateId}' for assignment change (using pre-fetched campaigns)");

                // Load templateName for this specific templateId from configuration
                string templateName = string.Empty;
                try
                {
                    var templatesSection = configuration.GetSection("LockerMailer:Templates");
                    foreach (var template in templatesSection.GetChildren())
                    {
                        var configTemplateId = template.GetValue<string>("templateId");
                        if (!string.IsNullOrEmpty(configTemplateId) && configTemplateId.Equals(templateId, StringComparison.OrdinalIgnoreCase))
                        {
                            templateName = template.GetValue<string>("templateName") ?? string.Empty;
                            logger.Debug($"Loaded templateName '{templateName}' for templateId '{templateId}'");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to load templateName for templateId '{templateId}'");
                }

                // Use pre-fetched campaigns instead of fetching again
                logger.Debug($"Using {preFetchedCampaigns.Count} pre-fetched campaigns from Keila");
                var campaign = preFetchedCampaigns.FirstOrDefault(c =>
                    c.Id.Equals(templateId, StringComparison.OrdinalIgnoreCase) ||
                    c.Subject.Equals(templateId, StringComparison.OrdinalIgnoreCase));

                if (campaign == null)
                {
                    logger.Warning($"Template '{templateId}' not found among pre-fetched Keila campaigns");
                    return;
                }

                // Build HTML from blocks (each block's data.text becomes a paragraph)
                var sb = new System.Text.StringBuilder();
                sb.Append("<html><body>");

                if (campaign.JsonBody?.Blocks != null)
                {
                    foreach (var block in campaign.JsonBody.Blocks)
                    {
                        var rawText = block.Data?.Text ?? string.Empty;
                        var processedText = ReplaceTemplatePlaceholders(rawText, change, templateId);

                        sb.Append("<p>").Append(processedText).Append("</p>");
                    }
                }

                sb.Append("</body></html>");
                var htmlEmail = sb.ToString();

                // For now, print the HTML to the terminal for debugging
                logger.Information("Generated HTML email:\n" + htmlEmail);

                // In DebugMode, only log the HTML; otherwise send
                var debugMode = configuration.GetValue<bool>("LockerMailer:DebugMode", false);
                if (!debugMode)
                {
                    // Determine recipient and subject based on change type
                    var recipientEmail = change.ChangeType.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)
                        ? change.PreviousAssignedEmployeeEmail
                        : change.NewAssignedEmployeeEmail;

                    if (!string.IsNullOrWhiteSpace(recipientEmail))
                    {
                        var subject = !string.IsNullOrEmpty(templateName) ? templateName : (campaign.Subject ?? templateId);
                        await smtpMailAdapter.SendAsync(recipientEmail, subject, htmlEmail);
                        logger.Information($"Email sent to {recipientEmail} with subject '{subject}' for locker '{change.LockerName}' change {change.ChangeType}");
                    }
                    else
                    {
                        logger.Warning($"No recipient email available for locker '{change.LockerName}' change {change.ChangeType}");
                    }
                }

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error processing template '{templateId}' with assignment data");
                throw;
            }
        }
    }
}