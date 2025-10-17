using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Serilog;
using Innovatrics.SmartFace.Integrations.LockerMailer;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    /// <summary>
    /// Demo class to show Keila integration functionality
    /// </summary>
    public class KeilaIntegrationDemo
    {
        public static async Task RunDemo()
        {
            // Setup logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                Log.Information("=== KEILA INTEGRATION DEMO ===");
                
                // Setup configuration
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // Setup services
                var services = new ServiceCollection();
                services.AddHttpClient();
                services.AddSingleton<IConfiguration>(configuration);
                services.AddSingleton<ILogger>(Log.Logger);
                services.AddTransient<IKeilaDataAdapter, KeilaDataAdapter>();

                var serviceProvider = services.BuildServiceProvider();

                // Get the adapter
                var keilaAdapter = serviceProvider.GetRequiredService<IKeilaDataAdapter>();

                Log.Information("1. Fetching Keila campaigns on startup...");
                var campaigns = await keilaAdapter.GetCampaignsWithTemplatesAsync();
                Log.Information($"   ✅ Successfully fetched {campaigns.Count} campaigns");

                Log.Information("2. Displaying campaign details:");
                foreach (var campaign in campaigns)
                {
                    Log.Information($"   📧 Campaign: {campaign.Subject} (ID: {campaign.Id})");
                    Log.Information($"      Updated: {campaign.UpdatedAt}");
                    Log.Information($"      Template ID: {campaign.TemplateId}");
                    
                    if (campaign.JsonBody?.Blocks != null)
                    {
                        Log.Information($"      Blocks ({campaign.JsonBody.Blocks.Count}):");
                        foreach (var block in campaign.JsonBody.Blocks)
                        {
                            var textPreview = string.IsNullOrEmpty(block.Data?.Text) 
                                ? "No text" 
                                : block.Data.Text.Substring(0, Math.Min(80, block.Data.Text.Length));
                            Log.Information($"        - {block.Type}: {textPreview}...");
                        }
                    }
                }

                Log.Information("3. Simulating periodic refresh (every 60 seconds)...");
                var refreshPeriod = configuration.GetValue<int>("LockerMailer:Connections:Keila:KeilaRefreshPeriodMs", 60000);
                Log.Information($"   Refresh period: {refreshPeriod}ms ({refreshPeriod/1000} seconds)");

                Log.Information("4. Simulating template processing:");
                var testChange = new AssignmentChange
                {
                    LockerName = "Locker-001",
                    NewAssignedEmployeeName = "John Doe",
                    ChangeType = "courier_delivery",
                    ChangeTimestamp = DateTime.Now
                };

                var templateToUse = campaigns.FirstOrDefault(c => c.Subject.Equals("lockers-flow_2", StringComparison.OrdinalIgnoreCase));
                if (templateToUse != null)
                {
                    Log.Information($"   Using template: {templateToUse.Subject}");
                    
                    if (templateToUse.JsonBody?.Blocks != null)
                    {
                        foreach (var block in templateToUse.JsonBody.Blocks)
                        {
                            if (block.Data?.Text != null)
                            {
                                var processedText = block.Data.Text;
                                processedText = processedText.Replace("{{ campaign.data.fullname }}", testChange.NewAssignedEmployeeName);
                                processedText = processedText.Replace("{{ campaign.data.time }}", DateTime.Now.ToString("HH:mm"));
                                processedText = processedText.Replace("{{ campaign.data.source }}", testChange.LockerName);
                                processedText = processedText.Replace("{{ campaign.data.canceltime }}", "18:00");

                                Log.Information($"   Processed: {processedText.Substring(0, Math.Min(100, processedText.Length))}...");
                            }
                        }
                    }
                }

                Log.Information("5. Integration features implemented:");
                Log.Information("   ✅ Keila API connection with Bearer token authentication");
                Log.Information("   ✅ Campaign data fetching and caching");
                Log.Information("   ✅ Periodic refresh using KeilaRefreshPeriodMs configuration");
                Log.Information("   ✅ Template variable processing");
                Log.Information("   ✅ Debug logging with campaign details");
                Log.Information("   ✅ Integration with MainHostedService for automatic startup");

                Log.Information("=== DEMO COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Demo failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
