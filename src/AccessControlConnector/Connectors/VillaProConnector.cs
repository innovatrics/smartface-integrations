using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Serilog;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories;
using Innovatrics.SmartFace.Models.API;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors
{
    public class VillaProConnector : IAccessControlConnector
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private string authToken;
        private string systToken;
        private string baseUrl;

        public VillaProConnector(ILogger logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        
            authToken = configuration.GetValue<string>("VillaProConfiguration:authToken") ?? throw new InvalidOperationException("authToken is required");
            systToken = configuration.GetValue<string>("VillaProConfiguration:systToken") ?? throw new InvalidOperationException("systToken is required");
            baseUrl = configuration.GetValue<string>("VillaProConfiguration:baseUrl") ?? throw new InvalidOperationException("baseUrl is required");
        }
        
        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null,string username = null, string password = null)
        {
            return Task.CompletedTask;
        }
        
        public async Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            this.logger.Information($"Initiating OpenAsync: VillaProConnector: {accessControlMapping.Username}({accessControlUserId}), using stream mapping: {accessControlMapping.StreamId}, for the gate: {accessControlMapping.TargetId}");
            
            // Implementation for opening the gate
            await TicketPassAsync(accessControlMapping.TargetId, accessControlUserId, authToken, systToken);
        }

        public async Task TicketPassAsync(string deviceId, string ticketId, string authToken, string systToken)
        {
            try
            {
                this.logger.Information($"VillaProConnector: Initiating ticket pass for device {deviceId} with ticket {ticketId}");
                
                var client = CreateHttpClient();
                var url = $"{baseUrl}/devices/{deviceId}/ticket_pass/";
                
                var request = new HttpRequestMessage(HttpMethod.Put, url);
                request.Headers.Add("auth-token", authToken);
                request.Headers.Add("syst-token", systToken);
                
                var content = new StringContent(
                    JsonConvert.SerializeObject(new { ticket_id = ticketId }),
                    Encoding.UTF8,
                    "application/json");
                
                request.Content = content;
                
                var response = await client.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    this.logger.Information($"VillaProConnector: Ticket pass successful for device {deviceId} with ticket {ticketId}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    this.logger.Error($"VillaProConnector: Ticket pass failed for device {deviceId} with ticket {ticketId}. Status: {response.StatusCode}, Error: {errorContent}");
                    throw new Exception($"Ticket pass failed with status code {response.StatusCode}: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"VillaProConnector: Error during ticket pass for device {deviceId} with ticket {ticketId}");
                throw;
            }
        }

        public async Task TicketIssueAndPassAsync(string deviceId, string ticketId, string authToken, string systToken)
        {
            try
            {
                this.logger.Information($"VillaProConnector: Initiating ticket issue and pass for device {deviceId} with ticket {ticketId}");
                
                var client = CreateHttpClient();
                var url = $"{baseUrl}/devices/{deviceId}/ticket_issue_and_pass/";
                
                var request = new HttpRequestMessage(HttpMethod.Put, url);
                request.Headers.Add("auth-token", authToken);
                request.Headers.Add("syst-token", systToken);
                
                var content = new StringContent(
                    JsonConvert.SerializeObject(new { ticket_id = ticketId }),
                    Encoding.UTF8,
                    "application/json");
                
                request.Content = content;
                
                var response = await client.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    this.logger.Information($"VillaProConnector: Ticket issue and pass successful for device {deviceId} with ticket {ticketId}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    this.logger.Error($"VillaProConnector: Ticket issue and pass failed for device {deviceId} with ticket {ticketId}. Status: {response.StatusCode}, Error: {errorContent}");
                    throw new Exception($"Ticket issue and pass failed with status code {response.StatusCode}: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"VillaProConnector: Error during ticket issue and pass for device {deviceId} with ticket {ticketId}");
                throw;
            }
        }

        private HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();

            return new HttpClient(handler);
        }
    }
}
