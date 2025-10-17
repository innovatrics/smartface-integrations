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

        // Fields for tracking assignment changes
        private Dictionary<long, long?> _previousLockerAssignments = new Dictionary<long, long?>();
        private DateTime? _lastAssignmentCheckTime = null;
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
        }

        public async Task ProcessEmailSummaryAssignmentChanges(EmailSummaryResponse emailSummary)
        {
            try
            {
                logger.Information($"Processing {emailSummary.TotalChanges} assignment changes");

                // Get cached Keila campaigns for template processing
                var keilaCampaigns = await keilaDataAdapter.GetCampaignsWithTemplatesAsync();
                logger.Information($"Retrieved {keilaCampaigns.Count} Keila campaigns for template processing");

                // Process each assignment change
                foreach (var change in emailSummary.Changes)
                {
                    logger.Information($"Processing change: Locker {change.LockerName} assigned from {change.PreviousAssignedEmployeeName} to {change.NewAssignedEmployeeName}");

                    // Determine which template to use based on the change type or locker function
                    var templateToUse = DetermineTemplateForChange(change, keilaCampaigns);
                    
                    if (templateToUse != null)
                    {
                        logger.Information($"Using template: {templateToUse.Subject} (ID: {templateToUse.Id})");
                        
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

        private KeilaCampaign? DetermineTemplateForChange(AssignmentChange change, List<KeilaCampaign> campaigns)
        {
            // Map change types to template subjects based on your configuration
            string templateSubject = change.ChangeType.ToLower() switch
            {
                "food_delivery" or "food" => "lockers-flow_1",
                "courier_delivery" or "courier" => "lockers-flow_2", 
                "courier_reminder" or "reminder" => "lockers-flow_3",
                _ => "lockers-flow_2" // Default to courier delivery
            };

            return campaigns.FirstOrDefault(c => c.Subject.Equals(templateSubject, StringComparison.OrdinalIgnoreCase));
        }

        private async Task ProcessTemplateWithAssignmentData(KeilaCampaign campaign, AssignmentChange change)
        {
            try
            {
                logger.Information($"Processing template '{campaign.Subject}' for assignment change");

                if (campaign.JsonBody?.Blocks != null)
                {
                    foreach (var block in campaign.JsonBody.Blocks)
                    {
                        if (block.Data?.Text != null)
                        {
                            // Replace template variables with actual data
                            var processedText = block.Data.Text
                                .Replace("{{ campaign.data.fullname }}", change.NewAssignedEmployeeName)
                                .Replace("{{ campaign.data.time }}", DateTime.Now.ToString("HH:mm"))
                                .Replace("{{ campaign.data.source }}", change.LockerName)
                                .Replace("{{ campaign.data.canceltime }}", "18:00"); // Default to 6 PM

                            logger.Information($"Processed block text: {processedText.Substring(0, Math.Min(100, processedText.Length))}...");
                            
                            // Here you would typically:
                            // 1. Send the processed email via SMTP
                            // 2. Log the email sending
                            // 3. Handle any errors
                        }
                    }
                }

                await Task.CompletedTask; // Placeholder for async operations
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error processing template '{campaign.Subject}' with assignment data");
                throw;
            }
        }

    }
}