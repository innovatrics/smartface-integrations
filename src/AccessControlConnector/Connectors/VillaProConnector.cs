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
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        private string authToken;
        private string systToken;
        private string baseUrl;

        public VillaProConnector(ILogger logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        
            authToken = configuration.GetValue<string>("VillaProConfiguration:AuthToken") ?? throw new InvalidOperationException("authToken is required");
            systToken = configuration.GetValue<string>("VillaProConfiguration:SystToken") ?? throw new InvalidOperationException("systToken is required");
            baseUrl = configuration.GetValue<string>("VillaProConfiguration:BaseUrl") ?? throw new InvalidOperationException("baseUrl is required");

            _logger.Information($"VillaProConnector: {authToken} - {systToken} - {baseUrl}");
            
            if (string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(systToken) || string.IsNullOrEmpty(baseUrl))
            {
                _logger.Error("VillaProConnector: Missing required configuration values");
                throw new InvalidOperationException("Missing required configuration values");
            }
        }
        
        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null,string username = null, string password = null)
        {
            return Task.CompletedTask;
        }
        
        public async Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            _logger.Information($"Initiating OpenAsync: VillaProConnector: {accessControlMapping.Username}({accessControlUserId}), using stream mapping: {accessControlMapping.StreamId}, for the gate: {accessControlMapping.TargetId}");
            
            // Implementation for opening the gate
            await TicketPassAsync(accessControlMapping.TargetId, accessControlUserId, authToken, systToken);
        }

        public async Task TicketPassAsync(string deviceId, string ticketId, string authToken, string systToken)
        {
            try
            {
                _logger.Information($"VillaProConnector: Initiating ticket pass for device {deviceId} with ticket {ticketId}");
                
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
                    _logger.Information($"VillaProConnector: {response.StatusCode} - Ticket pass sent successfully for device {deviceId} with ticket {ticketId}");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.Error($"VillaProConnector: Ticket pass failed with server error {response.StatusCode} for device {deviceId} with ticket {ticketId}. Error: {errorContent}");
                    throw new Exception($"Ticket pass failed with server error {response.StatusCode}: {errorContent}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.Error($"VillaProConnector: Ticket pass failed for device {deviceId} with ticket {ticketId}. Status: {response.StatusCode}, Error: {errorContent}");
                    throw new Exception($"Ticket pass failed with status code {response.StatusCode}: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"VillaProConnector: Error during ticket pass for device {deviceId} with ticket {ticketId}");
                throw;
            }
        }

        private HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();

            return new HttpClient(handler);
        }

        public Task DenyAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            return Task.CompletedTask;
        }

        public Task BlockAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            return Task.CompletedTask;
        }
    }
}
