using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;
using Innovatrics.SmartFace.Integrations.LockerMailer;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;
using System.Text.Json;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class DashBoardsDataAdapter : IDashboardsDataAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private readonly string DataSource;
        private string DashboardsHost;
        private int DashboardsPort;
        private string DashboardsUsername;
        private string DashboardsPassword;
        private string DashboardsIntegrationIdentifierType;
        private Dictionary<string, bool> DefaultTemplates = new();

        private AeosWebServiceTypeClient? client;
        private HttpClient httpClient;

        public DashBoardsDataAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            this.logger.Information("Aeos Dashboard DataAdapter Initiated");

            DataSource = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:DataSource") ?? string.Empty;
            DashboardsHost = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:Host") ?? string.Empty;
            DashboardsPort = configuration.GetValue<int>("LockerMailer:Connections:Dashboards:Port");
            DashboardsUsername = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:User") ?? string.Empty;
            DashboardsPassword = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:Pass") ?? string.Empty;
            DashboardsIntegrationIdentifierType = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:IntegrationIdentifierType") ?? string.Empty;
            
            // Debug configuration values
            this.logger.Information($"DashboardsHost: '{DashboardsHost}', DashboardsPort: {DashboardsPort}");
            this.logger.Debug($"DashboardsUsername: '{DashboardsUsername}'");
            this.logger.Debug($"DashboardsPassword: '{(string.IsNullOrEmpty(DashboardsPassword) ? "EMPTY" : "SET")}'");
            
            // Validate configuration values
            if (string.IsNullOrEmpty(DashboardsHost))
            {
                this.logger.Error("DashboardsHost configuration is missing or empty");
                throw new InvalidOperationException("DashboardsHost configuration is missing. Please check 'LockerMailer:Connections:Dashboards:Host' in appsettings.json");
            }
            
            if (DashboardsPort <= 0)
            {
                this.logger.Error($"DashboardsPort configuration is invalid: {DashboardsPort}");
                throw new InvalidOperationException("DashboardsPort configuration is invalid. Please check 'LockerMailer:Connections:Dashboards:Port' in appsettings.json");
            }
            
            // Initialize HTTP client for REST API calls
            httpClient = httpClientFactory.CreateClient();
            
            // Set up basic authentication if credentials are provided
            if (!string.IsNullOrEmpty(DashboardsUsername) && !string.IsNullOrEmpty(DashboardsPassword))
            {
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{DashboardsUsername}:{DashboardsPassword}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
            
            // add here connection to Aoes Dashboards
            if (!string.IsNullOrEmpty(DashboardsHost))
            {
                var endpoint = new Uri($"{DashboardsHost}:{DashboardsPort}");
                var endpointBinding = new BasicHttpBinding()
                {
                    MaxBufferSize = int.MaxValue,
                    ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max,
                    MaxReceivedMessageSize = int.MaxValue,
                    AllowCookies = true,
                    Security =
                    {
                        Mode = (endpoint.Scheme == "https") ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None,
                        Transport =
                        {
                            ClientCredentialType = HttpClientCredentialType.Basic
                        }
                    }
                };
                var endpointAddress = new EndpointAddress(endpoint);

                client = new AeosWebServiceTypeClient(endpointBinding, endpointAddress);
                client.ClientCredentials.ServiceCertificate.SslCertificateAuthentication = new X509ServiceCertificateAuthentication()
                {
                    CertificateValidationMode = X509CertificateValidationMode.None,
                    RevocationMode = X509RevocationMode.NoCheck
                };
                client.ClientCredentials.UserName.UserName = DashboardsUsername;
                client.ClientCredentials.UserName.Password = DashboardsPassword;
            }
        }

        public async Task<EmailSummaryResponse> GetEmailSummaryAssignmentChanges()
        {
            try
            {
                
                
                var baseUrl = $"{DashboardsHost}:{DashboardsPort}";
                var endpoint = $"{baseUrl}/api/lockeranalytics/email-summary/assignment-changes";
                this.logger.Information($"Fetching email summary assignment changes from Dashboards API. Calling endpoint: {endpoint}");
                
                var response = await httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                this.logger.Debug($"Received response: {content}");
                
                var result = JsonSerializer.Deserialize<EmailSummaryResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                this.logger.Information($"Successfully retrieved {result?.TotalChanges ?? 0} assignment changes");
                return result ?? new EmailSummaryResponse();
            }
            catch (HttpRequestException ex)
            {
                this.logger.Error(ex, "HTTP request failed when calling Dashboards API");
                throw;
            }
            catch (JsonException ex)
            {
                this.logger.Error(ex, "Failed to deserialize response from Dashboards API");
                throw;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Unexpected error occurred while fetching email summary assignment changes");
                throw;
            }
        }

        public async Task<List<GroupInfo>> GetGroups()
        {
            try
            {
                this.logger.Information("Fetching groups from Dashboards API");
                
                var baseUrl = $"{DashboardsHost}:{DashboardsPort}";
                var endpoint = $"{baseUrl}/api/lockeranalytics/groups";
                
                this.logger.Information($"Calling endpoint: {endpoint}");
                
                var response = await httpClient.GetAsync(endpoint);
                
                // Log response details for debugging
                this.logger.Information($"Response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    this.logger.Error($"API returned error status {response.StatusCode}: {errorContent}");
                    
                    // Return empty response instead of throwing exception for now
                    this.logger.Warning($"Returning empty groups response due to API error");
                    return new List<GroupInfo>();
                }
                
                var content = await response.Content.ReadAsStringAsync();
                this.logger.Debug($"Received response: {content}");
                
                var result = JsonSerializer.Deserialize<List<GroupInfo>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                this.logger.Information($"Successfully retrieved {result?.Count ?? 0} groups from Dashboards API");
                return result ?? new List<GroupInfo>();
            }
            catch (HttpRequestException ex)
            {
                this.logger.Error(ex, "HTTP request failed when calling groups API");
                // Return empty response instead of throwing to allow the application to continue
                this.logger.Warning($"Returning empty groups response due to HTTP error");
                return new List<GroupInfo>();
            }
            catch (JsonException ex)
            {
                this.logger.Error(ex, "Failed to deserialize groups response from Dashboards API");
                // Return empty response instead of throwing to allow the application to continue
                this.logger.Warning($"Returning empty groups response due to JSON parsing error");
                return new List<GroupInfo>();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Unexpected error occurred while fetching groups");
                // Return empty response instead of throwing to allow the application to continue
                this.logger.Warning($"Returning empty groups response due to unexpected error");
                return new List<GroupInfo>();
            }
        }

        public async Task<LockerReleaseResult> ReleaseLockerAsync(int lockerId)
        {
            var result = new LockerReleaseResult { LockerId = lockerId };
            
            try
            {
                var baseUrl = $"{DashboardsHost}:{DashboardsPort}";
                var endpoint = $"{baseUrl}/api/lockeranalytics/release-locker/{lockerId}";
                
                this.logger.Information($"Releasing locker {lockerId} via Dashboards API. Calling endpoint: POST {endpoint}");
                
                var response = await httpClient.PostAsync(endpoint, null);
                result.StatusCode = (int)response.StatusCode;
                
                if (response.IsSuccessStatusCode)
                {
                    result.Success = true;
                    result.Message = $"Locker {lockerId} successfully released";
                    this.logger.Information($"[Locker Release] SUCCESS - Locker ID: {lockerId}, Status Code: {result.StatusCode}");
                }
                else
                {
                    result.Success = false;
                    var errorContent = await response.Content.ReadAsStringAsync();
                    result.Message = $"Failed to release locker {lockerId}: {errorContent}";
                    
                    // Log specific error codes as per requirements
                    switch (result.StatusCode)
                    {
                        case 400:
                            this.logger.Error($"[Locker Release] FAILED - Bad Request - Locker ID: {lockerId}, Status Code: 400, Details: {errorContent}");
                            break;
                        case 403:
                            this.logger.Error($"[Locker Release] FAILED - Forbidden - Locker ID: {lockerId}, Status Code: 403, Details: {errorContent}");
                            break;
                        case 500:
                            this.logger.Error($"[Locker Release] FAILED - Internal Server Error - Locker ID: {lockerId}, Status Code: 500, Details: {errorContent}");
                            break;
                        default:
                            this.logger.Error($"[Locker Release] FAILED - Locker ID: {lockerId}, Status Code: {result.StatusCode}, Details: {errorContent}");
                            break;
                    }
                }
                
                return result;
            }
            catch (HttpRequestException ex)
            {
                result.Success = false;
                result.StatusCode = 0;
                result.Message = $"HTTP request failed for locker {lockerId}: {ex.Message}";
                this.logger.Error(ex, $"[Locker Release] FAILED - HTTP Error - Locker ID: {lockerId}, Exception: {ex.Message}");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.StatusCode = 0;
                result.Message = $"Unexpected error releasing locker {lockerId}: {ex.Message}";
                this.logger.Error(ex, $"[Locker Release] FAILED - Unexpected Error - Locker ID: {lockerId}, Exception: {ex.Message}");
                return result;
            }
        }

        public async Task<List<LockerAccessEvent>> GetAccessedLockersAsync(DateTime? fromDateTime = null)
        {
            try
            {
                var baseUrl = $"{DashboardsHost}:{DashboardsPort}";
                var endpoint = $"{baseUrl}/api/lockeranalytics/accessedLockers";
                
                if (fromDateTime.HasValue)
                {
                    var dateTimeStr = fromDateTime.Value.ToString("o"); // ISO 8601 format
                    endpoint = $"{endpoint}?dateTime={Uri.EscapeDataString(dateTimeStr)}";
                }
                
                this.logger.Information($"Fetching accessed lockers from Dashboards API. Calling endpoint: GET {endpoint}");
                
                var response = await httpClient.GetAsync(endpoint);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    this.logger.Error($"API returned error status {response.StatusCode}: {errorContent}");
                    return new List<LockerAccessEvent>();
                }
                
                var content = await response.Content.ReadAsStringAsync();
                this.logger.Debug($"Received accessed lockers response: {content}");
                
                var result = JsonSerializer.Deserialize<List<LockerAccessEvent>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                this.logger.Information($"Successfully retrieved {result?.Count ?? 0} locker access events");
                return result ?? new List<LockerAccessEvent>();
            }
            catch (HttpRequestException ex)
            {
                this.logger.Error(ex, "HTTP request failed when calling accessedLockers API");
                return new List<LockerAccessEvent>();
            }
            catch (JsonException ex)
            {
                this.logger.Error(ex, "Failed to deserialize accessedLockers response from Dashboards API");
                return new List<LockerAccessEvent>();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Unexpected error occurred while fetching accessed lockers");
                return new List<LockerAccessEvent>();
            }
        }
    }
}
