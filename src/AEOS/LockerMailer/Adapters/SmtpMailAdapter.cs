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

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class SmtpMailAdapter : ISmtpMailAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private readonly string DataSource;
        private string KeilaEndpoint;
        private int KeilaServerPageSize;
        private string KeilaUsername;
        private string KeilaPassword;
        private string KeilaIntegrationIdentifierType;
        private Dictionary<string, bool> DefaultTemplates = new();

        private AeosWebServiceTypeClient client;

        public SmtpMailAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            this.logger.Information("SmtpMail DataAdapter Initiated");

            DataSource = configuration.GetValue<string>("LockerMailer:Connections:Keila:DataSource");
            KeilaEndpoint = configuration.GetValue<string>("LockerMailer:Connections:Keila:Host");
            KeilaUsername = configuration.GetValue<string>("LockerMailer:Connections:Keila:User");
            KeilaPassword = configuration.GetValue<string>("LockerMailer:Connections:Keila:Pass");
            KeilaServerPageSize = configuration.GetValue<int>("LockerMailer:Connections:Keila:ServerPageSize", 100);
            KeilaIntegrationIdentifierType = configuration.GetValue<string>("LockerMailer:Connections:Keila:IntegrationIdentifierType");
            
            if (!string.IsNullOrEmpty(KeilaEndpoint))
            {
                var endpoint = new Uri(KeilaEndpoint);
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
                client.ClientCredentials.UserName.UserName = KeilaUsername;
                client.ClientCredentials.UserName.Password = KeilaPassword;
            }
        }

       
    }
}
