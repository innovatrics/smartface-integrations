using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Serilog;
using Innovatrics.SmartFace.Integrations.LockerMailer;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.Tests
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Setup logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
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

                Log.Information("Testing Keila API connection...");

                // Fetch campaigns
                var campaignsResponse = await keilaAdapter.GetCampaignsAsync();

                Log.Information($"Total campaigns found: {campaignsResponse.Meta.Count}");
                Log.Information($"Page: {campaignsResponse.Meta.Page} of {campaignsResponse.Meta.PageCount}");

                // Display interesting data for each campaign
                foreach (var campaign in campaignsResponse.Data)
                {
                    Log.Information($"\n--- Campaign: {campaign.Subject} ---");
                    Log.Information($"ID: {campaign.Id}");
                    Log.Information($"Updated: {campaign.UpdatedAt}");
                    Log.Information($"Template ID: {campaign.TemplateId}");
                    
                    if (campaign.JsonBody?.Blocks != null)
                    {
                        Log.Information($"Blocks count: {campaign.JsonBody.Blocks.Count}");
                        foreach (var block in campaign.JsonBody.Blocks)
                        {
                            var textPreview = string.IsNullOrEmpty(block.Data.Text) 
                                ? "No text" 
                                : block.Data.Text.Substring(0, Math.Min(100, block.Data.Text.Length));
                            Log.Information($"  Block Type: {block.Type}, Text: {textPreview}...");
                        }
                    }
                }

                Log.Information("\nTest completed successfully!");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred during Keila connection test");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
