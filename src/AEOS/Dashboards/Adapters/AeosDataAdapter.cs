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
            DataSource = configuration.GetValue<string>("AeosDashboards:DataSource");
            AeosEndpoint = configuration.GetValue<string>("AeosDashboards:Aeos:Server:Wsdl") ?? throw new InvalidOperationException("The AEOS SOAP API URL is not read.");
            AeosUsername = configuration.GetValue<string>("AeosDashboards:Aeos:Server:User") ?? throw new InvalidOperationException("The AEOS username is not read.");
            AeosPassword = configuration.GetValue<string>("AeosDashboards:Aeos:Server:Pass") ?? throw new InvalidOperationException("The AEOS password is not read.");
            
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
            this.logger.Information("Receiving Lockers from AEOS");

            List<AeosLockers> AeosAllLockers = new List<AeosLockers>();

            bool allLockers = false;
            int LockersPageSize = AeosServerPageSize;
            int LockersPageNumber = 0;

            while (allLockers == false)
            {
                LockersPageNumber += 1;
                var lockerSearchInfo = new LockerSearchInfo();
                
                // Create LockerSearch object first
                lockerSearchInfo.LockerSearch = new LockerSearch();
                
                // Then create SearchRange
                lockerSearchInfo.SearchRange = new SearchRange();
                lockerSearchInfo.SearchRange.startRecordNo = (LockersPageNumber - 1) * LockersPageSize;
                lockerSearchInfo.SearchRange.nrOfRecords = LockersPageSize;
                lockerSearchInfo.SearchRange.nrOfRecordsSpecified = true;

                var lockers = await client.findLockerAsync(lockerSearchInfo);

                if (lockers?.LockerList?.Locker == null || !lockers.LockerList.Locker.Any())
                {
                    allLockers = true;
                    continue;
                }

                foreach (var locker in lockers.LockerList.Locker)
                {
                    this.logger.Debug($"{locker.Id}, {locker.Name}, {locker.LastUsed}, {locker.AssignedTo} ");
                    AeosAllLockers.Add(new AeosLockers(locker.Id, locker.Name, locker.LastUsed, locker.AssignedTo));
                }

                // If we received fewer records than requested, we've reached the end
                if (lockers.LockerList.Locker.Length < LockersPageSize)
                {
                    allLockers = true;
                }
            }

            return AeosAllLockers;
        }

    }
}
