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

        private AeosWebServiceTypeClient client;
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

            DataSource = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:DataSource");
            DashboardsHost = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:Host");
            DashboardsPort = configuration.GetValue<int>("LockerMailer:Connections:Dashboards:Port");
            DashboardsUsername = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:User");
            DashboardsPassword = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:Pass");
            DashboardsIntegrationIdentifierType = configuration.GetValue<string>("LockerMailer:Connections:Dashboards:IntegrationIdentifierType");
            
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
                var endpoint = new Uri($"http://{DashboardsHost}:{DashboardsPort}");
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
                this.logger.Debug("Fetching email summary assignment changes from Dashboards API");
                
                var baseUrl = $"http://{DashboardsHost}:{DashboardsPort}";
                var endpoint = $"{baseUrl}/api/lockeranalytics/email-summary/assignment-changes";
                
                this.logger.Debug($"Calling endpoint: {endpoint}");
                
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
    }
}
