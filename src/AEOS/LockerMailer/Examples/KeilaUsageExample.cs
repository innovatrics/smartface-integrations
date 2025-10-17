using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Serilog;
using Innovatrics.SmartFace.Integrations.LockerMailer;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.Examples
{
    /// <summary>
    /// Example showing how to use the KeilaDataAdapter to fetch campaign data
    /// </summary>
    public class KeilaUsageExample
    {
        private readonly IKeilaDataAdapter _keilaAdapter;
        private readonly ILogger _logger;

        public KeilaUsageExample(IKeilaDataAdapter keilaAdapter, ILogger logger)
        {
            _keilaAdapter = keilaAdapter;
            _logger = logger;
        }

        /// <summary>
        /// Example method showing how to fetch and process Keila campaigns
        /// </summary>
        public async Task ProcessKeilaCampaignsAsync()
        {
            try
            {
                _logger.Information("Starting to process Keila campaigns...");

                // Fetch all campaigns from Keila API
                var campaigns = await _keilaAdapter.GetCampaignsWithTemplatesAsync();

                _logger.Information($"Retrieved {campaigns.Count} campaigns from Keila");

                // Process each campaign
                foreach (var campaign in campaigns)
                {
                    await ProcessCampaign(campaign);
                }

                _logger.Information("Finished processing Keila campaigns");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while processing Keila campaigns");
                throw;
            }
        }

        /// <summary>
        /// Process individual campaign data
        /// </summary>
        private async Task ProcessCampaign(KeilaCampaign campaign)
        {
            _logger.Information($"Processing campaign: {campaign.Subject} (ID: {campaign.Id})");

            // Extract interesting data as mentioned in requirements:
            // - json_body.blocks
            // - subject
            // - updated_at

            var campaignData = new
            {
                Id = campaign.Id,
                Subject = campaign.Subject,
                UpdatedAt = campaign.UpdatedAt,
                Blocks = campaign.JsonBody?.Blocks ?? new System.Collections.Generic.List<KeilaBlock>()
            };

            _logger.Information($"Campaign data extracted: Subject='{campaignData.Subject}', Updated='{campaignData.UpdatedAt}', Blocks={campaignData.Blocks.Count}");

            // Process blocks if they exist
            if (campaignData.Blocks.Count > 0)
            {
                foreach (var block in campaignData.Blocks)
                {
                    _logger.Information($"Block Type: {block.Type}, Text: {block.Data.Text}");
                    
                    // Here you can process the block data as needed
                    // For example, extract template variables like {{ campaign.data.fullname }}
                    ProcessBlockContent(block);
                }
            }

            // You can add more processing logic here based on your requirements
            await Task.CompletedTask; // Placeholder for async operations
        }

        /// <summary>
        /// Process individual block content
        /// </summary>
        private void ProcessBlockContent(KeilaBlock block)
        {
            if (string.IsNullOrEmpty(block.Data.Text))
                return;

            // Example: Extract template variables from the text
            var text = block.Data.Text;
            
            // Look for template variables like {{ campaign.data.fullname }}
            if (text.Contains("{{ campaign.data.fullname }}"))
            {
                _logger.Information("Found fullname template variable in block");
            }
            
            if (text.Contains("{{ campaign.data.time }}"))
            {
                _logger.Information("Found time template variable in block");
            }
            
            if (text.Contains("{{ campaign.data.source }}"))
            {
                _logger.Information("Found source template variable in block");
            }
        }

        /// <summary>
        /// Example of how to set up the KeilaDataAdapter in your application
        /// </summary>
        public static void SetupKeilaAdapter(IServiceCollection services, IConfiguration configuration)
        {
            // Add HTTP client factory
            services.AddHttpClient();
            
            // Register the KeilaDataAdapter
            services.AddTransient<IKeilaDataAdapter, KeilaDataAdapter>();
            
            // The adapter will automatically read configuration from:
            // - LockerMailer:Connections:Keila:Host
            // - LockerMailer:Connections:Keila:Port
            // - LockerMailer:Connections:Keila:ApiKey
            // etc.
        }
    }
}
