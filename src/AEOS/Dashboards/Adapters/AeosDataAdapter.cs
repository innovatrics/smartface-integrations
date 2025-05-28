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

            this.logger.Information("AeosDataAdapter Initiated");

            //var s = ((IConfigurationRoot)configuration).GetDebugView();
            DataSource = configuration.GetValue<string>("AeosDashboards:DataSource");
            AeosEndpoint = configuration.GetValue<string>("AeosDashboards:Aeos:Server:Wsdl") ?? throw new InvalidOperationException("The AEOS SOAP API URL is not read.");
            AeosUsername = configuration.GetValue<string>("AeosDashboards:Aeos:Server:User") ?? throw new InvalidOperationException("The AEOS username is not read.");
            AeosPassword = configuration.GetValue<string>("AeosDashboards:Aeos:Server:Pass") ?? throw new InvalidOperationException("The AEOS password is not read.");
            AeosServerPageSize = configuration.GetValue<int>("AeosDashboards:Aeos:Server:PageSize", 100); // Default to 100 if not specified
            
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

        public async Task<IList<AeosLockerGroups>> GetLockerGroups()
        {
            this.logger.Debug("Receiving Locker Groups from AEOS");

            List<AeosLockerGroups> AeosAllGroups = new List<AeosLockerGroups>();

            var lockerGroupSearchInfo = new LockerGroupSearchInfo();
            
            // Create LockerGroupSearch object first
            lockerGroupSearchInfo.LockerGroupSearch = new LockerGroupSearch();
            
            // Then create SearchRange
            lockerGroupSearchInfo.SearchRange = new SearchRange();
            lockerGroupSearchInfo.SearchRange.startRecordNo = 0;
            lockerGroupSearchInfo.SearchRange.nrOfRecords = AeosServerPageSize;
            lockerGroupSearchInfo.SearchRange.nrOfRecordsSpecified = true;

            var lockerGroups = await client.findLockerGroupAsync(lockerGroupSearchInfo);

            if (lockerGroups?.LockerGroupList?.LockerGroup == null)
            {
                this.logger.Error("No locker groups found in the response");
                return AeosAllGroups;
            }

            foreach (var group in lockerGroups.LockerGroupList.LockerGroup)
            {
                if (group == null)
                {
                    this.logger.Error("Null locker group found in the response");
                    continue;
                }

                var lockerIds = new List<long>();
                if (group.LockerIdList != null)
                {
                    foreach (var lockerId in group.LockerIdList)
                    {
                        lockerIds.Add(lockerId);
                    }
                }

                this.logger.Debug($"Processing locker group - Id: {group.Id}, Name: {group.Name}, Description: {group.Description}, Function: {group.LockerFunction}, Template: {group.LockerBehaviourTemplate}, Locker Count: {lockerIds.Count}");
                
                AeosAllGroups.Add(new AeosLockerGroups(
                    group.Id,
                    group.Name,
                    group.Description,
                    lockerIds,
                    group.LockerBehaviourTemplate,
                    group.LockerFunction
                ));
            }

            this.logger.Information($"Amount of Locker Groups found: {AeosAllGroups.Count}");
            return AeosAllGroups;
        }

        public async Task<IList<AeosMember>> GetEmployees()
        {
            this.logger.Debug("Receiving Employees from AEOS");

            List<AeosMember> AeosAllMembers = new List<AeosMember>();

            bool allEmployees = false;
            int EmployeesPageSize = AeosServerPageSize;
            int EmployeesPageNumber = 0;
            int EmployeesTotalCount = 0;

            List<AeosMember> AeosAllMembersReturn = new List<AeosMember>();

            while (allEmployees == false)
            {
                EmployeesPageNumber += 1;
                var employeeSearch = new EmployeeSearchInfo();
                employeeSearch.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();

                employeeSearch.SearchRange = new SearchRange();
                if (EmployeesPageNumber == 1)
                {
                    employeeSearch.SearchRange.startRecordNo = 0;
                }
                else
                {
                    employeeSearch.SearchRange.startRecordNo = (((EmployeesPageNumber) * (EmployeesPageSize)) - EmployeesPageSize);
                }

                //adding sort order
                employeeSearch.SortOrder = new PersonSortOrderItem[1];
                employeeSearch.SortOrder[0] = new PersonSortOrderItem();
                employeeSearch.SortOrder[0].Order = SortDirection.A;
                employeeSearch.SortOrder[0].FieldName = PersonSortFields.LastName;

                employeeSearch.SearchRange.nrOfRecords = EmployeesPageSize;
                employeeSearch.SearchRange.nrOfRecordsSpecified = true;

                var employees = await client.findEmployeeAsync(employeeSearch);

                foreach (var employee in employees.EmployeeList.Employee)
                {
                    this.logger.Debug($"employee.EmployeeInfo.FirstName: {employee.EmployeeInfo.FirstName}, employee.EmployeeInfo.LastName: {employee.EmployeeInfo.LastName}");
                    AeosAllMembersReturn.Add(new AeosMember(employee.EmployeeInfo.Id, employee.EmployeeInfo.FirstName, employee.EmployeeInfo.LastName));
                }

                if (employees.EmployeeList.Employee.Length == EmployeesPageSize)
                {
                    this.logger.Debug($"End of page {EmployeesPageNumber}. Amount of Employees found: {employees.EmployeeList.Employee.Length}. Number of results match the pagination limit. Another page will be checked.");
                    EmployeesTotalCount = EmployeesTotalCount + employees.EmployeeList.Employee.Length;
                }
                else
                {
                    allEmployees = true;
                    this.logger.Debug($"End of last page {EmployeesPageNumber}. Amount of Employees found: {employees.EmployeeList.Employee.Length}.");
                    EmployeesTotalCount = EmployeesTotalCount + employees.EmployeeList.Employee.Length;
                    break;
                }
            }
            this.logger.Information($"Amount of Employees found: {EmployeesTotalCount}");
            return AeosAllMembersReturn;
        }

    }
}
