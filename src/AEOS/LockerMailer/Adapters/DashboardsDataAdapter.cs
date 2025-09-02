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

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class DashBoardsDataAdapter : IDashBoardsDataAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private readonly string DataSource;
        private string DashboardsEndpoint;
        private int DashboardsServerPageSize;
        private string DashboardsUsername;
        private string DashboardsPassword;
        private string DashboardsIntegrationIdentifierType;
        private Dictionary<string, bool> DefaultTemplates = new();

        private AeosWebServiceTypeClient client;

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

            DataSource = configuration.GetValue<string>("LockerMailer:Dashboards:DataSource");
            DashboardsEndpoint = configuration.GetValue<string>("LockerMailer:Dashboards:Endpoint");
            DashboardsUsername = configuration.GetValue<string>("LockerMailer:Dashboards:Username");
            DashboardsPassword = configuration.GetValue<string>("LockerMailer:Dashboards:Password");
            DashboardsServerPageSize = configuration.GetValue<int>("LockerMailer:Dashboards:ServerPageSize", 100);
            DashboardsIntegrationIdentifierType = configuration.GetValue<string>("LockerMailer:Dashboards:IntegrationIdentifierType");
            // add here connection to Aoes Dashboards
            
            var endpoint = new Uri(DashboardsEndpoint);
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


        public async Task<IList<AeosLockers>> GetLockers()
        {
            this.logger.Debug("Receiving Lockers from AEOS");

            List<AeosLockers> AeosAllLockers = new List<AeosLockers>();

            bool allLockers = false;
            int LockersPageSize = AeosServerPageSize;
            int LockersPageNumber = 0;
            int LockersTotalCount = 0;

            while (allLockers == false)
            {
                
                LockersPageNumber += 1;
                this.logger.Debug($"Receiving Lockers from AEOS: Page {LockersPageNumber}");
                var lockerSearch = new LockerSearchInfo();
                
                // Create LockerSearch object first
                lockerSearch.LockerSearch = new LockerSearch();
                
                // Then create SearchRange
                lockerSearch.SearchRange = new SearchRange();

                if (LockersPageNumber == 1)
                {
                    lockerSearch.SearchRange.startRecordNo = 0;
                }
                else
                {
                    lockerSearch.SearchRange.startRecordNo = (((LockersPageNumber) * (LockersPageSize)) - LockersPageSize);
                }

                lockerSearch.SearchRange.startRecordNo = (LockersPageNumber - 1) * LockersPageSize;
                lockerSearch.SearchRange.nrOfRecords = LockersPageSize;
                lockerSearch.SearchRange.nrOfRecordsSpecified = true;

                var lockers = await client.findLockerAsync(lockerSearch);

                if (lockers == null)
                {
                    this.logger.Error("Null response received from findLockerAsync");
                    continue;
                }

            
                if (lockers?.LockerList == null)
                {
                    this.logger.Error("LockerList is null in the response");
                    continue;
                }

                if (lockers.LockerList.Locker == null)
                {
                    this.logger.Error("Locker array is null in the response");
                    continue;
                }

                this.logger.Debug($"Processing {lockers.LockerList.Locker.Length} lockers from page {LockersPageNumber}");

                foreach (var locker in lockers.LockerList.Locker)
                {
                    if (locker == null)
                    {
                        this.logger.Error("Null locker object found in the array");
                        continue;
                    }

                    this.logger.Debug($"Processing locker - Id: {locker.Id}, Name: {locker.Name}, LastUsed: {locker.LastUsed}, AssignedTo: {locker.AssignedTo}, Location: {locker.Location}, HostName: {locker.HostName}, Online: {locker.Online}, LockerFunction: {locker.LockerFunction}, LockerGroupId: {locker.LockerGroupId}");
                    AeosAllLockers.Add(new AeosLockers(locker.Id, locker.Name, locker.LastUsed, locker.AssignedTo, locker.Location, locker.HostName, locker.Online, locker.LockerFunction, locker.LockerGroupId));
                }

                if (lockers.LockerList.Locker.Length == AeosServerPageSize)
                {
                    this.logger.Debug($"End of page {LockersPageNumber}. Amount of Lockers found: {lockers.LockerList.Locker.Length}. Number of results match the pagination limit. Another page will be checked.");
                    LockersTotalCount = LockersTotalCount + lockers.LockerList.Locker.Length;
                }
                else
                {
                    allLockers = true;
                    this.logger.Debug($"End of last page {LockersPageNumber}. Amount of Lockers found: {lockers.LockerList.Locker.Length}.");
                    LockersTotalCount = LockersTotalCount + lockers.LockerList.Locker.Length;
                    break;
                }
            }

            this.logger.Information($"Amount of Lockers found: {LockersTotalCount}");
            return AeosAllLockers;
        }

}
}
