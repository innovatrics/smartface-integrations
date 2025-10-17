using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;
using System.Collections.Generic;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class MainHostedService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IDataOrchestrator bridge;
        private readonly IDashboardsDataAdapter dashboardsDataAdapter;
        private readonly IKeilaDataAdapter keilaDataAdapter;

        private int SyncPeriodMs = new int();
        private int KeilaRefreshPeriodMs = new int();
        private DateTime lastSync;
        private DateTime lastKeilaSync;
        private List<KeilaCampaign> cachedKeilaCampaigns = new List<KeilaCampaign>();

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            IDataOrchestrator bridge,
            IDashboardsDataAdapter dashboardsDataAdapter,
            IKeilaDataAdapter keilaDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            this.dashboardsDataAdapter = dashboardsDataAdapter ?? throw new ArgumentNullException(nameof(dashboardsDataAdapter));
            this.keilaDataAdapter = keilaDataAdapter ?? throw new ArgumentNullException(nameof(keilaDataAdapter));

            SyncPeriodMs = configuration.GetValue<int>("LockerMailer:RefreshPeriodMs", 300000); // Default to 5 minutes
            KeilaRefreshPeriodMs = configuration.GetValue<int>("LockerMailer:Connections:Keila:KeilaRefreshPeriodMs", 300000); // Default to 5 minutes
            
            if(SyncPeriodMs == 0)
            {
                throw new InvalidOperationException($"The RefreshPeriodMs needs to be greater than 0. Current value: {SyncPeriodMs}");
            }
            
            if(KeilaRefreshPeriodMs == 0)
            {
                throw new InvalidOperationException($"The KeilaRefreshPeriodMs needs to be greater than 0. Current value: {KeilaRefreshPeriodMs}");
            }
            
            lastSync = DateTime.UtcNow;
            lastKeilaSync = DateTime.UtcNow;
        }
         
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Initial Keila data fetch on startup
            await FetchAndCacheKeilaCampaigns();
            
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var currentTime = DateTime.UtcNow;
                    
                    // Check if we need to refresh Keila data
                    if (currentTime - lastKeilaSync >= TimeSpan.FromMilliseconds(KeilaRefreshPeriodMs))
                    {
                        await FetchAndCacheKeilaCampaigns();
                        lastKeilaSync = currentTime;
                    }
                    
                    // Check if we need to refresh Dashboards data
                    if (currentTime - lastSync >= TimeSpan.FromMilliseconds(SyncPeriodMs))
                    {
                        // Get email summary assignment changes from Dashboards API
                        var emailSummary = await this.dashboardsDataAdapter.GetEmailSummaryAssignmentChanges();
                        this.logger.Information($"Retrieved {emailSummary.TotalChanges} assignment changes from Dashboards API");
                        
                        // Process the data through the orchestrator
                        await this.bridge.ProcessEmailSummaryAssignmentChanges(emailSummary);
                        lastSync = currentTime;
                    }
                    
                    // Wait for a short period before checking again
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    this.logger.Information($"The Service is being shut.");
                }
                catch (Exception e)
                {
                    this.logger.Error(e, "The sync tool failed unexpectedly.");
                }
            }           
        }

        private async Task FetchAndCacheKeilaCampaigns()
        {
            try
            {
                this.logger.Information("Fetching Keila campaigns...");
                var campaigns = await this.keilaDataAdapter.GetCampaignsWithTemplatesAsync();
                
                // Update cached campaigns
                cachedKeilaCampaigns = campaigns;
                
                this.logger.Information($"Successfully cached {campaigns.Count} Keila campaigns");
                
                // Debug logging - print campaign details
                var debugMode = configuration.GetValue<bool>("LockerMailer:DebugMode", false);
                if (debugMode)
                {
                    this.logger.Information("=== KEILA CAMPAIGNS DEBUG INFO ===");
                    foreach (var campaign in campaigns)
                    {
                        this.logger.Information($"Campaign: {campaign.Subject} (ID: {campaign.Id})");
                        this.logger.Information($"  Updated: {campaign.UpdatedAt}");
                        this.logger.Information($"  Template ID: {campaign.TemplateId}");
                        
                        if (campaign.JsonBody?.Blocks != null)
                        {
                            this.logger.Information($"  Blocks ({campaign.JsonBody.Blocks.Count}):");
                            foreach (var block in campaign.JsonBody.Blocks)
                            {
                                var textPreview = string.IsNullOrEmpty(block.Data?.Text) 
                                    ? "No text" 
                                    : block.Data.Text.Substring(0, Math.Min(100, block.Data.Text.Length));
                                this.logger.Information($"    - {block.Type}: {textPreview}...");
                            }
                        }
                        this.logger.Information("  ---");
                    }
                    this.logger.Information("=== END KEILA CAMPAIGNS DEBUG INFO ===");
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to fetch Keila campaigns");
            }
        }

        public List<KeilaCampaign> GetCachedKeilaCampaigns()
        {
            return cachedKeilaCampaigns;
        }

        public KeilaCampaign? GetKeilaCampaignBySubject(string subject)
        {
            return cachedKeilaCampaigns.FirstOrDefault(c => c.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase));
        }
    }
}