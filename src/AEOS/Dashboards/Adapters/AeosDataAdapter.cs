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

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public class AeosDataAdapter : IAeosDataAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private readonly string DataSource;
        private string AeosEndpoint;
        private int AeosServerPageSize;
        private string AeosUsername;
        private string AeosPassword;
        private Dictionary<string, bool> DefaultTemplates = new();

        private AeosWebServiceTypeClient client;

        public AeosDataAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            this.logger.Debug("AeosDataAdapter Initiated");

            //var s = ((IConfigurationRoot)configuration).GetDebugView();
            DataSource = configuration.GetValue<string>("AeosSync:DataSource");
            AeosEndpoint = configuration.GetValue<string>("AeosSync:Aeos:Server:Wsdl") ?? throw new InvalidOperationException("The AEOS SOAP API URL is not read.");
            AeosUsername = configuration.GetValue<string>("AeosSync:Aeos:Server:User") ?? throw new InvalidOperationException("The AEOS username is not read.");
            AeosPassword = configuration.GetValue<string>("Aeossync:Aeos:Server:Pass") ?? throw new InvalidOperationException("The AEOS password is not read.");
            
            var endpoint = new Uri(AeosEndpoint);
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
            client.ClientCredentials.UserName.UserName = AeosUsername;
            client.ClientCredentials.UserName.Password = AeosPassword;
        }


        public async Task<IList<AeosLockers>> GetLockers()
        {
            this.logger.Debug("Receiving Lockers from AEOS");

            List<AeosLockers> AeosAllLockers = new List<AeosLockers>();

            bool allLockers = false;
            int LockersPageSize = AeosServerPageSize;
            int LockersPageNumber = 0;

            while (allLockers == false)
            {
                LockersPageNumber += 1;
                var lockerSearch = new LockerSearchInfo();
                lockerSearch.SearchRange = new SearchRange();
                lockerSearch.SearchRange.startRecordNo = 0;
                lockerSearch.SearchRange.nrOfRecords = LockersPageSize;

                if (LockersPageNumber == 1)
                {
                    lockerSearch.SearchRange.startRecordNo = 0;
                }
                else
                {
                    lockerSearch.SearchRange.startRecordNo = (LockersPageNumber * LockersPageSize) - LockersPageSize;
                }

                lockerSearch.SearchRange.nrOfRecords = LockersPageSize;
                lockerSearch.SearchRange.nrOfRecordsSpecified = true;

                var lockers = await client.findLockerAsync(lockerSearch);

                foreach (var locker in lockers.LockerList.Locker)
                {
                    this.logger.Information($"{locker.Id}, {locker.Name}, {locker.LastUsed}, {locker.AssignedTo} ");
                    AeosAllLockers.Add(new AeosLockers(locker.Id, locker.Name, locker.LastUsed, locker.AssignedTo));
                }
            }

            return AeosAllLockers;
        }

    }
}
