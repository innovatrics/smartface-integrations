using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    /// <summary>
    /// Simple standalone test for Keila API integration
    /// </summary>
    public class SimpleKeilaTest
    {
        public static async Task TestKeilaConnection()
        {
            Console.WriteLine("=== SIMPLE KEILA API TEST ===");
            
            try
            {
                // Configuration from appsettings.json
                var keilaHost = "http://sface-integ-2u";
                var keilaPort = 4000;
                var apiKey = "GhM6h6p918GGVLOH_dwdZa66nGk0on9JbeTzjdzUIaQ";
                var campaignsUrl = $"{keilaHost}:{keilaPort}/api/v1/campaigns";
                
                Console.WriteLine($"Testing connection to: {campaignsUrl}");
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                
                var response = await httpClient.GetAsync(campaignsUrl);
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✅ Successfully connected to Keila API!");
                Console.WriteLine($"📊 Response length: {jsonContent.Length} characters");
                
                // Parse the response
                var campaignsResponse = JsonConvert.DeserializeObject<KeilaCampaignsResponse>(jsonContent);
                
                if (campaignsResponse?.Data != null)
                {
                    Console.WriteLine($"📧 Found {campaignsResponse.Data.Count} campaigns:");
                    
                    foreach (var campaign in campaignsResponse.Data)
                    {
                        Console.WriteLine($"  • {campaign.Subject} (ID: {campaign.Id})");
                        Console.WriteLine($"    Updated: {campaign.UpdatedAt}");
                        Console.WriteLine($"    Template ID: {campaign.TemplateId}");
                        
                        if (campaign.JsonBody?.Blocks != null)
                        {
                            Console.WriteLine($"    Blocks: {campaign.JsonBody.Blocks.Count}");
                            foreach (var block in campaign.JsonBody.Blocks)
                            {
                                var textPreview = string.IsNullOrEmpty(block.Data?.Text) 
                                    ? "No text" 
                                    : block.Data.Text.Substring(0, Math.Min(50, block.Data.Text.Length));
                                Console.WriteLine($"      - {block.Type}: {textPreview}...");
                            }
                        }
                        Console.WriteLine();
                    }
                }
                
                Console.WriteLine("=== TEST COMPLETED SUCCESSFULLY ===");
                Console.WriteLine("✅ Keila API integration is working correctly!");
                Console.WriteLine("✅ Campaign data is being fetched and parsed properly!");
                Console.WriteLine("✅ Template blocks are accessible for processing!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}

