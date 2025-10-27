using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;
using Innovatrics.SmartFace.Integrations.AEOS.SmartFaceClients;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;
using Newtonsoft.Json;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class KeilaDataAdapter : IKeilaDataAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private string KeilaEndpoint;
        private string KeilaHost;
        private int KeilaPort;
        private string KeilaUsername = string.Empty;
        private string KeilaPassword = string.Empty;
        private string KeilaApiKey = string.Empty;   
        private AeosWebServiceTypeClient client;

        public KeilaDataAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            this.logger.Information("Keila DataAdapter Initiated");
            
            // Debug configuration loading
            logger.Information($"Current working directory: {Environment.CurrentDirectory}");
            logger.Information($"Configuration sources count: {configuration.AsEnumerable().Count()}");
            
            KeilaHost = configuration.GetValue<string>("LockerMailer:Connections:Keila:Host") ?? string.Empty;
            KeilaPort = configuration.GetValue<int>("LockerMailer:Connections:Keila:Port");
            
            logger.Information($"Read KeilaHost: '{KeilaHost}'");
            logger.Information($"Read KeilaPort: {KeilaPort}");
            
            // Validate configuration values
            if (string.IsNullOrEmpty(KeilaHost))
            {
                logger.Error("Keila Host configuration is null or empty");
                logger.Error($"Configuration path 'LockerMailer:Connections:Keila:Host' returned: '{KeilaHost}'");
                throw new InvalidOperationException("Keila Host configuration is missing or empty. Please check 'LockerMailer:Connections:Keila:Host' in appsettings.json");
            }
            
            KeilaEndpoint = $"{KeilaHost}:{KeilaPort}";
            logger.Information($"KeilaEndpoint constructed as: {KeilaEndpoint}");
            
            KeilaUsername = configuration.GetValue<string>("LockerMailer:Connections:Keila:User") ?? string.Empty;
            KeilaPassword = configuration.GetValue<string>("LockerMailer:Connections:Keila:Pass") ?? string.Empty; 
            KeilaApiKey = configuration.GetValue<string>("LockerMailer:Connections:Keila:ApiKey") ?? string.Empty;
            // add here connection to Aoes Dashboards
            
            // Create proper URI for SOAP endpoint
            logger.Information($"Attempting to create URI from: {KeilaEndpoint}");
            var soapEndpoint = new Uri(KeilaEndpoint);
            var endpointBinding = new BasicHttpBinding()
            {
                MaxBufferSize = int.MaxValue,
                ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max,
                MaxReceivedMessageSize = int.MaxValue,
                AllowCookies = true,
                Security =
                {
                    Mode = (soapEndpoint.Scheme == "https") ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None,
                    Transport =
                    {
                        ClientCredentialType = HttpClientCredentialType.Basic
                    }
                }
            };
            var endpointAddress = new EndpointAddress(soapEndpoint);

            client = new AeosWebServiceTypeClient(endpointBinding, endpointAddress);
            client.ClientCredentials.ServiceCertificate.SslCertificateAuthentication = new X509ServiceCertificateAuthentication()
            {
                CertificateValidationMode = X509CertificateValidationMode.None,
                RevocationMode = X509RevocationMode.NoCheck
            };
            client.ClientCredentials.UserName.UserName = KeilaUsername;
            client.ClientCredentials.UserName.Password = KeilaPassword;
        }

        public async Task<KeilaCampaignsResponse> GetCampaignsAsync()
        {
            try
            {
                logger.Debug("Fetching campaigns from Keila API");
                
                using var httpClient = httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", KeilaApiKey);
                
                var campaignsUrl = $"{KeilaEndpoint}/api/v1/campaigns";
                logger.Information($"Making request to: {campaignsUrl}");
                
                var response = await httpClient.GetAsync(campaignsUrl);
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                logger.Debug($"Received response from Keila API: {jsonContent.Length} characters");
                
                var campaignsResponse = JsonConvert.DeserializeObject<KeilaCampaignsResponse>(jsonContent);
                
                if (campaignsResponse?.Data != null)
                {
                    logger.Information($"Successfully fetched {campaignsResponse.Data.Count} campaigns from Keila");
                    
                    // Log interesting data for each campaign
                    foreach (var campaign in campaignsResponse.Data)
                    {
                        logger.Debug($"Campaign ID: {campaign.Id}, Subject: {campaign.Subject}, Updated: {campaign.UpdatedAt} with {(campaign.JsonBody?.Blocks != null ? campaign.JsonBody.Blocks.Count : 0)} blocks");
                        
                    }
                }
                else
                {
                    logger.Warning("No campaign data received from Keila API");
                }
                
                return campaignsResponse ?? new KeilaCampaignsResponse();
            }
            catch (HttpRequestException ex)
            {
                logger.Error(ex, "HTTP error occurred while fetching campaigns from Keila API");
                throw;
            }
            catch (JsonException ex)
            {
                logger.Error(ex, "JSON deserialization error while processing Keila API response");
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unexpected error occurred while fetching campaigns from Keila API");
                throw;
            }
        }

        public async Task<List<KeilaCampaign>> GetCampaignsWithTemplatesAsync()
        {
            var campaignsResponse = await GetCampaignsAsync();
            return campaignsResponse.Data ?? new List<KeilaCampaign>();
        }

}
}
