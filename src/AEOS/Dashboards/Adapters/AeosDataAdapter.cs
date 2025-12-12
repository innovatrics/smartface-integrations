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

        private readonly string? DataSource;
        private string AeosEndpoint;
        private int AeosServerPageSize;
        private string AeosUsername;
        private string AeosPassword;
        private string AeosIntegrationIdentifierType;
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

            this.logger.Information("Aeos Dashboard DataAdapter Initiated");

            //var s = ((IConfigurationRoot)configuration).GetDebugView();
            DataSource = configuration.GetValue<string>("AeosDashboards:DataSource");
            AeosEndpoint = configuration.GetValue<string>("AeosDashboards:Aeos:Server:Wsdl") ?? throw new InvalidOperationException("The AEOS SOAP API URL is not read.");
            AeosUsername = configuration.GetValue<string>("AeosDashboards:Aeos:Server:User") ?? throw new InvalidOperationException("The AEOS username is not read.");
            AeosPassword = configuration.GetValue<string>("AeosDashboards:Aeos:Server:Pass") ?? throw new InvalidOperationException("The AEOS password is not read.");
            AeosServerPageSize = configuration.GetValue<int>("AeosDashboards:Aeos:Server:PageSize", 100); // Default to 100 if not specified
            AeosIntegrationIdentifierType = configuration.GetValue<string>("AeosDashboards:Aeos:Integration:SmartFace:IdentifierType") ?? throw new InvalidOperationException("The AEOS integration identifier type is not read.");
            
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

            this.logger.Debug($"Amount of Lockers found: {LockersTotalCount}");
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

            this.logger.Debug($"Amount of Locker Groups found: {AeosAllGroups.Count}");
            return AeosAllGroups;
        }

        public async Task<IList<ServiceReference.LockerAuthorisationGroupInfo>> GetLockerAuthorisationGroups()
        {
            this.logger.Debug("Receiving Locker Authorisation Groups from AEOS");

            List<ServiceReference.LockerAuthorisationGroupInfo> allAuthGroups = new List<ServiceReference.LockerAuthorisationGroupInfo>();

            var authGroupSearchInfo = new LockerAuthorisationGroupSearchInfo();
            authGroupSearchInfo.LockerAuthorisationGroupSearch = new LockerAuthorisationGroupSearch();
            authGroupSearchInfo.SearchRange = new SearchRange();
            authGroupSearchInfo.SearchRange.startRecordNo = 0;
            authGroupSearchInfo.SearchRange.nrOfRecords = AeosServerPageSize;
            authGroupSearchInfo.SearchRange.nrOfRecordsSpecified = true;

            bool allAuthGroupsRetrieved = false;
            int pageNumber = 0;

            while (!allAuthGroupsRetrieved)
            {
                pageNumber += 1;
                this.logger.Debug($"Receiving Locker Authorisation Groups from AEOS: Page {pageNumber}");

                if (pageNumber > 1)
                {
                    authGroupSearchInfo.SearchRange.startRecordNo = (pageNumber - 1) * AeosServerPageSize;
                }

                var authGroups = await client.findLockerAuthorisationGroupAsync(authGroupSearchInfo);

                if (authGroups?.LockerAuthorisationGroupList?.LockerAuthorisationGroup == null)
                {
                    this.logger.Debug("No locker authorisation groups found in the response");
                    break;
                }

                foreach (var authGroup in authGroups.LockerAuthorisationGroupList.LockerAuthorisationGroup)
                {
                    if (authGroup != null)
                    {
                        allAuthGroups.Add(authGroup);
                        this.logger.Debug($"Processing locker authorisation group - Id: {authGroup.Id}, Name: {authGroup.Name}, LockerGroupCount: {authGroup.LockerGroupIdList?.Length ?? 0}");
                    }
                }

                if (authGroups.LockerAuthorisationGroupList.LockerAuthorisationGroup.Length < AeosServerPageSize)
                {
                    allAuthGroupsRetrieved = true;
                }
            }

            this.logger.Debug($"Amount of Locker Authorisation Groups found: {allAuthGroups.Count}");
            return allAuthGroups;
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
                    AeosAllMembersReturn.Add(new AeosMember(
                        employee.EmployeeInfo.Id,
                        employee.EmployeeInfo.FirstName,
                        employee.EmployeeInfo.LastName,
                        employee.EmployeeInfo.Email
                    ));
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
            this.logger.Debug($"Amount of Employees found: {EmployeesTotalCount}");
            return AeosAllMembersReturn;
        }

        public async Task<IList<AeosIdentifierType>> GetIdentifierTypes()
        {
            this.logger.Debug("Receiving Identifier Types from AEOS");

            List<AeosIdentifierType> AeosAllIdentifierTypes = new List<AeosIdentifierType>();

            
            var identifierTypeSearchInfo = new IdentifierTypeInfo();
            var identifierType = await client.findIdentifierTypeAsync(identifierTypeSearchInfo);

            if (identifierType == null)
            {
                this.logger.Error("No identifier types found in the response");
                return AeosAllIdentifierTypes;
            }

            if (identifierType.IdentifierTypeList == null)
            {
                this.logger.Error("IdentifierTypeList is null in the response");
                return AeosAllIdentifierTypes;
            }

            foreach (var type in identifierType.IdentifierTypeList)
            {
                this.logger.Debug($"identifierType.Id: {type.Id}, identifierType.Name: {type.Name}");
                AeosAllIdentifierTypes.Add(new AeosIdentifierType(type.Id, type.Name));
            }
            this.logger.Debug($"Amount of Identifier Types found: {AeosAllIdentifierTypes.Count}");
            return AeosAllIdentifierTypes;

    }

    public async Task<IList<AeosIdentifier>> GetIdentifiersPerType(long identifierType)
    {
        this.logger.Debug($"Receiving Identifiers per Type from AEOS");

         List<AeosIdentifier> AeosAllIdentifiers = new List<AeosIdentifier>();

        var identifierTypeSearchInfo = new IdentifierSearchInfo();
        identifierTypeSearchInfo.IdentifierSearch = new IdentifierSearch();
        identifierTypeSearchInfo.IdentifierSearch.IdentifierType = identifierType;
        identifierTypeSearchInfo.IdentifierSearch.IdentifierTypeSpecified = true;
        var identifiers = await client.findTokenAsync(identifierTypeSearchInfo);

        if (identifiers?.IdentifierAndCarrierIdList?.IdentifierAndCarrierId == null)
        {
            this.logger.Error("No identifiers found in the response");
            return AeosAllIdentifiers;
        }

        foreach (var identifier in identifiers.IdentifierAndCarrierIdList.IdentifierAndCarrierId)
        {
            this.logger.Debug($"identifier.Id: {identifier.Identifier.Id}, IdentifierType: {identifier.Identifier.IdentifierType}, identifier.BadgeNumber: {identifier.Identifier.BadgeNumber}, identifier.Blocked: {identifier.Identifier.Blocked}, identifier.CarrierId: {identifier.CarrierId}");
            AeosAllIdentifiers.Add(new AeosIdentifier(identifier.Identifier.Id, identifier.Identifier.BadgeNumber, identifier.Identifier.Blocked, identifier.CarrierId, identifier.Identifier.IdentifierType));
            
        }
        this.logger.Debug($"Amount of Identifiers found: {AeosAllIdentifiers.Count}");
        return AeosAllIdentifiers;
    }

    public async Task<IList<AeosMember>> GetEmployeesByIdentifier(string identifier)
    {
        this.logger.Debug($"Searching for employees with identifier: {identifier}");
        
        // Get all identifier types first
        var identifierTypes = await GetIdentifierTypes();
        var identifierTypeId = identifierTypes.FirstOrDefault(type => type.Name == AeosIntegrationIdentifierType)?.Id;
        
        if (identifierTypeId == null)
        {
            this.logger.Error($"Identifier type not found: {identifier}");
            return new List<AeosMember>();
        }
        this.logger.Information($"Identifier type found: {identifierTypeId}");

        // Create search criteria for tokens
        var tokenSearch = new IdentifierSearchInfo();
        tokenSearch.IdentifierSearch = new IdentifierSearch();
        tokenSearch.IdentifierSearch.IdentifierType = identifierTypeId.Value;
        tokenSearch.IdentifierSearch.IdentifierTypeSpecified = true;

        // Set up pagination
        tokenSearch.SearchRange = new SearchRange();
        tokenSearch.SearchRange.startRecordNo = 0;
        tokenSearch.SearchRange.nrOfRecords = AeosServerPageSize;
        tokenSearch.SearchRange.nrOfRecordsSpecified = true;

        var tokens = await client.findTokenAsync(tokenSearch);
        
        if (tokens?.IdentifierAndCarrierIdList?.IdentifierAndCarrierId == null)
        {
            this.logger.Error("No tokens found in the response");
            return new List<AeosMember>();
        }

        var result = new List<AeosMember>();
        foreach (var token in tokens.IdentifierAndCarrierIdList.IdentifierAndCarrierId)
        {
            if (token?.CarrierId == null)
            {
                this.logger.Error("Null carrier ID found in token response");
                continue;
            }

            // Get employee info for this carrier ID
            var employeeSearch = new EmployeeSearchInfo();
            employeeSearch.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();
            employeeSearch.EmployeeInfo.Id = token.CarrierId;
            employeeSearch.EmployeeInfo.IdSpecified = true;

            var employees = await client.findEmployeeAsync(employeeSearch);
            
            if (employees?.EmployeeList?.Employee == null || employees.EmployeeList.Employee.Length == 0)
            {
                this.logger.Error($"No employee found for carrier ID {token.CarrierId}");
                continue;
            }

            var employee = employees.EmployeeList.Employee[0];
            if (employee?.EmployeeInfo == null)
            {
                this.logger.Error($"Null employee info found for carrier ID {token.CarrierId}");
                continue;
            }

            this.logger.Debug($"Found employee - Id: {employee.EmployeeInfo.Id}, Name: {employee.EmployeeInfo.FirstName} {employee.EmployeeInfo.LastName}");
            result.Add(new AeosMember(
                employee.EmployeeInfo.Id,
                employee.EmployeeInfo.FirstName,
                employee.EmployeeInfo.LastName,
                employee.EmployeeInfo.Email
            ));
        }

        this.logger.Information($"Found {result.Count} employees with identifier {identifier}");
        return result;
    }

    public async Task<AeosMember?> GetEmployeeByEmail(string email)
    {
        this.logger.Debug($"Searching for employee with email: {email}");
        
        var employeeSearch = new EmployeeSearchInfo();
        employeeSearch.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();
        employeeSearch.EmployeeInfo.Email = email;

        var employees = await client.findEmployeeAsync(employeeSearch);
        
        if (employees?.EmployeeList?.Employee == null || employees.EmployeeList.Employee.Length == 0)
        {
            this.logger.Warning($"No employee found with email: {email}");
            return null;
        }

        var employee = employees.EmployeeList.Employee[0];
        if (employee?.EmployeeInfo == null)
        {
            this.logger.Error($"Null employee info found for email: {email}");
            return null;
        }

        this.logger.Information($"Found employee - Id: {employee.EmployeeInfo.Id}, Name: {employee.EmployeeInfo.FirstName} {employee.EmployeeInfo.LastName}, Email: {employee.EmployeeInfo.Email}");
        return new AeosMember(
            employee.EmployeeInfo.Id,
            employee.EmployeeInfo.FirstName,
            employee.EmployeeInfo.LastName,
            employee.EmployeeInfo.Email
        );
    }

    public async Task<bool> ReleaseLocker(long lockerId)
    {
        this.logger.Information($"Releasing locker with ID: {lockerId}");
        
        try
        {
            var response = await client.releaseLockerAsync(lockerId);
            
            this.logger.Information($"Locker release result for locker ID {lockerId}: {response.LockerActionResult}");
            return response.LockerActionResult;
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, $"Error releasing locker with ID: {lockerId}");
            throw;
        }
    }

    public async Task<IList<ServiceReference.TemplateInfo>> GetTemplates(string unitOfAuthType)
    {
        this.logger.Debug($"Receiving Templates from AEOS with UnitOfAuthType: {unitOfAuthType}");

        List<ServiceReference.TemplateInfo> allTemplates = new List<ServiceReference.TemplateInfo>();

        bool allTemplatesRetrieved = false;
        int pageNumber = 0;

        while (!allTemplatesRetrieved)
        {
            pageNumber += 1;
            this.logger.Debug($"Receiving Templates from AEOS: Page {pageNumber}");

            var templateSearchInfo = new TemplateSearchInfo();
            templateSearchInfo.TemplateInfo = new TemplateInfo();
            templateSearchInfo.TemplateInfo.UnitOfAuthType = unitOfAuthType;
            
            templateSearchInfo.SearchRange = new SearchRange();
            templateSearchInfo.SearchRange.startRecordNo = (pageNumber - 1) * AeosServerPageSize;
            templateSearchInfo.SearchRange.nrOfRecords = AeosServerPageSize;
            templateSearchInfo.SearchRange.nrOfRecordsSpecified = true;

            var templates = await client.findTemplateAsync(templateSearchInfo);

            if (templates?.TemplateList?.Template == null)
            {
                this.logger.Debug("No templates found in the response");
                break;
            }

            foreach (var template in templates.TemplateList.Template)
            {
                if (template != null)
                {
                    allTemplates.Add(template);
                    var templateItemDetails = template.TemplateItem?
                        .Where(ti => ti != null && ti.AuthorisationType == AuthSubject.LockerAuthorisationGroup)
                        .Select(ti => $"SubjectId={ti.SubjectId}, NetworkId={(ti.LockerAuthorisationGroupNetworkIdSpecified ? ti.LockerAuthorisationGroupNetworkId.ToString() : "not specified")}, PresetId={(ti.LockerAuthorisationPresetIdSpecified ? ti.LockerAuthorisationPresetId.ToString() : "not specified")}")
                        .ToList() ?? new List<string>();
                    
                    this.logger.Debug($"Processing template - Id: {template.Id}, Name: {template.Name}, UnitOfAuthType: {template.UnitOfAuthType}, TemplateItemCount: {template.TemplateItem?.Length ?? 0}");
                    if (templateItemDetails.Any())
                    {
                        this.logger.Debug($"  LockerAuthorisationGroup TemplateItems: {string.Join("; ", templateItemDetails)}");
                    }
                }
            }

            if (templates.TemplateList.Template.Length < AeosServerPageSize)
            {
                allTemplatesRetrieved = true;
            }
        }

        this.logger.Debug($"Amount of Templates found: {allTemplates.Count}");
        return allTemplates;
    }
}
}
