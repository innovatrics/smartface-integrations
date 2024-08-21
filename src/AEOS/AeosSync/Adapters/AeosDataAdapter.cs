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

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public class AeosDataAdapter : IAeosDataAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private string AeosEndpoint;
        private int AeosServerPageSize;
        private string AeosUsername;
        private string AeosPassword;
        private string SmartFaceIdFreefield;
        private string SmartFaceIdentifier;
        private string KeepUserField;
        private string FirstNameOrder;
        private bool AutoBiometryEnablement;
        private string AutoBiometryPrefix;
        private string BiometricEnrollmentStatus;
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

            AeosEndpoint = configuration.GetValue<string>("aeossync:Aeos:Server:Wdsl") ?? throw new InvalidOperationException("The AEOS SOAP API URL is not read.");
            AeosUsername = configuration.GetValue<string>("aeossync:Aeos:Server:User") ?? throw new InvalidOperationException("The AEOS username is not read.");
            AeosPassword = configuration.GetValue<string>("aeossync:Aeos:Server:Pass") ?? throw new InvalidOperationException("The AEOS password is not read.");
            SmartFaceIdFreefield = configuration.GetValue<string>("aeossync:Aeos:Integration:Freefield") ?? throw new InvalidOperationException("The AEOS SmartFaceIdFreefield is not read.");
            SmartFaceIdentifier = configuration.GetValue<string>("aeossync:Aeos:Integration:Identifier") ?? throw new InvalidOperationException("The AEOS SmartFaceIdentifier is not read.");
            AeosServerPageSize = configuration.GetValue<int>("aeossync:Aeos:Server:PageSize");
            KeepUserField = configuration.GetValue<string>("aeossync:Aeos:Integration:SmartFaceKeepUser") ?? throw new InvalidOperationException("The AEOS SmartFaceKeepUser is not read.");
            configuration.Bind("aeossync:Aeos:Integration:DefaultTemplates", DefaultTemplates);
            FirstNameOrder = configuration.GetValue<string>("AeosSync:SmartFace:FirstNameOrder");
            AutoBiometryEnablement = configuration.GetValue<bool>("aeossync:Aeos:AutoBiometryEnablement");
            AutoBiometryPrefix = configuration.GetValue<string>("aeossync:Aeos:AutoBiometryPrefix");
            BiometricEnrollmentStatus = configuration.GetValue<string>("aeossync:Aeos:Integration:BiometricEnrollmentStatus");

            if (BiometricEnrollmentStatus == null)
            {
                throw new InvalidOperationException("BiometricEnrollmentStatus was not set.");
            }


            if (AutoBiometryEnablement && AutoBiometryPrefix == null)
            {
                throw new InvalidOperationException("AutoBiometryEnablement is enabled and the AutoBiometryPrefix is missing.");
            }

            if (AeosServerPageSize <= 0)
            {
                throw new InvalidOperationException("The SmartFace GraphQL Page Size needs to be greater than 0.");
            }

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

        public async Task<IList<AeosMember>> GetEmployees()
        {
            this.logger.Debug("Receiving Employees from AEOS");

            List<AeosMember> AeosAllMembers = new List<AeosMember>();

            bool allEmployees = false;
            int EmployeesPageSize = AeosServerPageSize;
            int EmployeesPageNumber = 0;

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

                foreach (var employee in employees.EmployeeList)
                {
                    this.logger.Debug($"employee.EmployeeInfo.FirstName, employee.EmployeeInfo.LastName");
                    this.logger.Debug($"{employee.EmployeeInfo.FirstName}, {employee.EmployeeInfo.LastName}");
                    if (employee.FirstPhoto != null)
                    {
                        AeosAllMembersReturn.Add(new AeosMember(employee.EmployeeInfo.Id, employee.EmployeeInfo.GetFreefieldValue(SmartFaceIdFreefield), employee.EmployeeInfo.FirstName, employee.EmployeeInfo.LastName, employee.FirstPhoto.Picture));
                    }
                    else
                    {
                        AeosAllMembersReturn.Add(new AeosMember(employee.EmployeeInfo.Id, employee.EmployeeInfo.GetFreefieldValue(SmartFaceIdFreefield), employee.EmployeeInfo.FirstName, employee.EmployeeInfo.LastName));
                    }
                }

                if (employees.EmployeeList.Length == EmployeesPageSize)
                {
                    this.logger.Debug($"End of page {EmployeesPageNumber}. Amount of Employees found: {employees.EmployeeList.Length}. Number of results match the pagination limit. Another page will be checked.");
                }
                else
                {
                    allEmployees = true;
                    this.logger.Debug($"End of last page {EmployeesPageNumber}. Amount of Employees found: {employees.EmployeeList.Length}.");
                    break;
                }
            }
            return AeosAllMembersReturn;
        }

        public async Task<bool> CreateEmployees(AeosMember aeosMember, long badgeIdentifierType, long freefieldDefinitionId)
        {

            var member = aeosMember;
            this.logger.Information($"Creating Employee {member.FirstName} {member.LastName} width id {member.SmartFaceId}");
            var encodedSmartFaceId = Encoding.UTF8.GetBytes(member.SmartFaceId);
            if (encodedSmartFaceId.Length > 28)
            {
                this.logger.Warning($"The ID is longer than supported (28 bytes). {member.SmartFaceId} has {encodedSmartFaceId.Length} bytes");
                return false;
            }

            var addEmployee = new addEmployee()
            {
                EmployeeAdd = new EmployeeInfo()
                {
                    CarrierType = "Employee",
                    Freefield = new[]
                    {
                        new FreeFieldInfo()
                        {
                            DefinitionId = freefieldDefinitionId,
                            Name = SmartFaceIdFreefield,
                            value = member.SmartFaceId
                        }
                    },
                    FirstName = member.FirstName,
                    LastName = member.LastName
                }
            };

            var RegisteringTemplates = new List<string>();

            if (DefaultTemplates.Count > 0)
            {
                this.logger.Debug($"DefaultTemplates[]: {string.Join(" ", DefaultTemplates.Select(i => i.Key))}");


                foreach (var item in DefaultTemplates)
                {
                    if (item.Value == true)
                    {
                        RegisteringTemplates.Add(new String(item.Key));
                    }
                }
                this.logger.Debug($"RegisteringTemplates[]: {string.Join(" ", RegisteringTemplates)}");
            }
            else
            {
                this.logger.Debug("DefaultTemplates is empty");
            }

            var RegisteringTemplatesId = new List<long>();

            var findTemplate = new findTemplate();
            findTemplate.TemplateSearchInfo = new TemplateSearchInfo();
            findTemplate.TemplateSearchInfo.TemplateInfo = new TemplateInfo();
            findTemplate.TemplateSearchInfo.TemplateInfo.UnitOfAuthType = UnitOfAuthType.Locker;

            var findLockerTemplate = await client.findTemplateAsync(findTemplate.TemplateSearchInfo);
            if (findLockerTemplate.TemplateList.Length > 0)
            {
                foreach (var item in findLockerTemplate.TemplateList)
                {
                    this.logger.Debug($"Template IDs found: {item.Name} {item.Id}");
                    if (RegisteringTemplates.Contains(item.Name))
                    {
                        this.logger.Debug($"Template ID to be assigned: {item.Name} {item.Id}");
                        RegisteringTemplatesId.Add(item.Id);
                    }
                }
            }

            var addEmployeeResponse = await client.addEmployeeAsync(addEmployee.EmployeeAdd);
            this.logger.Debug($"Adding employee {addEmployee.EmployeeAdd.LastName} {addEmployee.EmployeeAdd.FirstName} - {addEmployee.EmployeeAdd.Freefield[0].value}");
            if (addEmployeeResponse.EmployeeResult.Id != 0)
            {

                this.logger.Information($"Added user {AeosExtensions.JoinNames(member.FirstName, member.LastName, FirstNameOrder)} with ID = {addEmployeeResponse.EmployeeResult.Id}");

                if (RegisteringTemplatesId.Count() > 0)
                {
                    var changeCarrier = new changeCarrierProfile();
                    changeCarrier.ProfileChange = new ProfileInfo();
                    changeCarrier.ProfileChange.CarrierId = addEmployeeResponse.EmployeeResult.Id;
                    changeCarrier.ProfileChange.AuthorisationLocker = new AuthorisationLocker();
                    changeCarrier.ProfileChange.AuthorisationLocker.TemplateAuthorisation = new TemplateAuthorisationLocker[RegisteringTemplatesId.Count()];
                    this.logger.Debug($"RegisteringTemplatesId.Count() = {RegisteringTemplatesId.Count()}");
                    for (int x = 0; x < RegisteringTemplatesId.Count(); x++)
                    {
                        this.logger.Information($"RegisteringTemplatesId[{x}] = {RegisteringTemplatesId[x]} for Carrier ID = {changeCarrier.ProfileChange.CarrierId} - {AeosExtensions.JoinNames(member.FirstName, member.LastName, FirstNameOrder)}");
                        changeCarrier.ProfileChange.AuthorisationLocker.TemplateAuthorisation[x] = new TemplateAuthorisationLocker();
                        changeCarrier.ProfileChange.AuthorisationLocker.TemplateAuthorisation[x].TemplateId = RegisteringTemplatesId[x];
                        changeCarrier.ProfileChange.AuthorisationLocker.TemplateAuthorisation[x].Enabled = true;
                    }

                    try
                    {
                        var changeCarrierResponse = await client.changeCarrierProfileAsync(changeCarrier.ProfileChange);
                        if (changeCarrierResponse.ProfileResult.CarrierId == 0)
                        {
                            this.logger.Error($"It was not possible to add profile templates to registered user {AeosExtensions.JoinNames(member.FirstName, member.LastName, FirstNameOrder)} - FAIL");
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception(e + "Something went wrong while adding carrier profiles.");
                    }
                }
                var addIdentifier = new assignToken();
                addIdentifier.IdentifierAdd = new CarrierIdentifierData();
                addIdentifier.IdentifierAdd.CarrierId = addEmployeeResponse.EmployeeResult.Id;
                addIdentifier.IdentifierAdd.IdentifierType = badgeIdentifierType;
                addIdentifier.IdentifierAdd.BadgeNumber = member.SmartFaceId;

                var addIdentifierResponse = await client.assignTokenAsync(addIdentifier.IdentifierAdd);

                if (addIdentifierResponse.IdentifierResult.Id != 0)
                {
                    this.logger.Information($"Adding identifier to registered user {AeosExtensions.JoinNames(member.FirstName, member.LastName, FirstNameOrder)} - SUCCESS");
                    return true;
                }
                else
                {
                    this.logger.Error($"Adding identifier to registered user {AeosExtensions.JoinNames(member.FirstName, member.LastName, FirstNameOrder)} - FAIL");
                    return false;
                }
            }
            else
            {
                this.logger.Error($"No user was generated.");
                throw new InvalidOperationException("No user was generated.");
            }


        }

        public async Task<bool> UpdateEmployee(AeosMember member, long FreefieldDefinitionId)
        {
            this.logger.Information($"Updating Employee with ID = {member.SmartFaceId}, new name: {member.FirstName} {member.LastName}");

            var returnedUser = await GetEmployeeId(member.SmartFaceId, FreefieldDefinitionId);

            if (returnedUser != null)
            {
                this.logger.Information($"Found a user with this SmartFaceId: {member.SmartFaceId}: {returnedUser.EmployeeInfo.Id} {returnedUser.EmployeeInfo.FirstName} {returnedUser.EmployeeInfo.LastName}");
                var updateID = returnedUser.EmployeeInfo.Id;

                var updateEmployee = new changeEmployee();
                updateEmployee.EmployeeChange = new EmployeeInfo();
                updateEmployee.EmployeeChange.Id = updateID;
                updateEmployee.EmployeeChange.IdSpecified = true;
                updateEmployee.EmployeeChange.FirstName = member.FirstName;
                updateEmployee.EmployeeChange.LastName = member.LastName;
                updateEmployee.EmployeeChange.Freefield = new FreeFieldInfo[1];
                updateEmployee.EmployeeChange.Freefield[0] = new FreeFieldInfo();
                updateEmployee.EmployeeChange.Freefield[0].DefinitionId = FreefieldDefinitionId;
                updateEmployee.EmployeeChange.Freefield[0].Name = SmartFaceIdFreefield;
                updateEmployee.EmployeeChange.Freefield[0].value = member.SmartFaceId;

                var updateEmployeeResponse = await client.changeEmployeeAsync(updateEmployee.EmployeeChange);

                if (updateEmployeeResponse.EmployeeResult.Id != 0)
                {
                    this.logger.Information($"Update> user with SmartFaceID {member.SmartFaceId} has been updated under {updateID} with new name {member.FirstName} {member.LastName}");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

        }

        public async Task<bool> EnableBiometryOnEmployee(long memberId, long FreefieldDefinitionId, long badgeIdentifierType)
        {
            if (AutoBiometryEnablement)
            {
                var response = await this.GetEmployeeByAeosId(memberId);
                if (response != null)
                {
                    var updateEmployee = new changeEmployee();
                    updateEmployee.EmployeeChange = new EmployeeInfo();
                    updateEmployee.EmployeeChange.Id = response.Id;
                    updateEmployee.EmployeeChange.IdSpecified = true;
                    updateEmployee.EmployeeChange.FirstName = response.FirstName;
                    updateEmployee.EmployeeChange.LastName = response.LastName;
                    updateEmployee.EmployeeChange.Freefield = new FreeFieldInfo[1];
                    updateEmployee.EmployeeChange.Freefield[0] = new FreeFieldInfo();
                    updateEmployee.EmployeeChange.Freefield[0].DefinitionId = FreefieldDefinitionId;
                    updateEmployee.EmployeeChange.Freefield[0].Name = SmartFaceIdFreefield;
                    updateEmployee.EmployeeChange.Freefield[0].value = AutoBiometryPrefix + response.SmartFaceId;

                    var updateEmployeeResponse = await client.changeEmployeeAsync(updateEmployee.EmployeeChange);
                    if (updateEmployeeResponse == null)
                    {
                        this.logger.Warning($"It was not possible to add SmartFaceId.");
                        return false;
                    }
                    else
                    {
                        var addIdentifier = new assignToken();
                        addIdentifier.IdentifierAdd = new CarrierIdentifierData();
                        addIdentifier.IdentifierAdd.CarrierId = response.Id;
                        addIdentifier.IdentifierAdd.IdentifierType = badgeIdentifierType;
                        addIdentifier.IdentifierAdd.BadgeNumber = AutoBiometryPrefix + response.SmartFaceId;

                        var addIdentifierResponse = await client.assignTokenAsync(addIdentifier.IdentifierAdd);
                        if (addIdentifierResponse.IdentifierResult.Id != 0)
                        {
                            this.logger.Debug($"Adding identifier to registered user {AeosExtensions.JoinNames(response.FirstName, response.LastName, FirstNameOrder)} - SUCCESS");
                            return true;
                        }
                        else
                        {
                            this.logger.Error($"Adding identifier to registered user {AeosExtensions.JoinNames(response.FirstName, response.LastName, FirstNameOrder)} - FAIL");
                            return false;
                        }
                    }

                }
                else
                {
                    this.logger.Information("Biometry is already enabled.");
                    return false;
                }
            }
            else
            {
                return false;
            }

        }


        public async Task<AeosMember> GetEmployeeByAeosId(long employeeId)
        {

            var employeeSearch = new EmployeeSearchInfo();
            employeeSearch.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();
            employeeSearch.EmployeeInfo.Id = employeeId;
            employeeSearch.EmployeeInfo.IdSpecified = true;

            var employee = await client.findEmployeeAsync(employeeSearch);
            if (employee.EmployeeList.Count() == 0)
            {
                return null;
            }
            else
            {
                var memberID = employee.EmployeeList[0].EmployeeInfo.Id;
                var memberSmartFaceId = employee.EmployeeList[0].EmployeeInfo.Id.ToString();
                var memberFirstName = employee.EmployeeList[0].EmployeeInfo.FirstName;
                var memberLastName = employee.EmployeeList[0].EmployeeInfo.LastName;
                var memberPicture = employee.EmployeeList[0].FirstPhoto.Picture;

                if (memberPicture != null)
                {
                    return new AeosMember(memberID, memberSmartFaceId, memberFirstName, memberLastName, memberPicture);
                }
                else
                {
                    return new AeosMember(memberID, memberSmartFaceId, memberFirstName, memberLastName);
                }

            }

        }

        public async Task<bool> RemoveEmployee(AeosMember member, long FreefieldDefinitionId)
        {
            this.logger.Information($"Removing Employee with ID = {member.SmartFaceId}, new name: {member.FirstName} {member.LastName}");
            var returnedUser = await GetEmployeeId(member.SmartFaceId, FreefieldDefinitionId);
            if (returnedUser == null)
            {
                return false;
            }


            foreach (var item in returnedUser.EmployeeInfo.Freefield)
            {
                this.logger.Debug($"{item.value} {item.Name}");
            }

            this.logger.Information($"DELETE> Found a user with this SmartFaceId: {member.SmartFaceId}: {returnedUser.EmployeeInfo.Id} {returnedUser.EmployeeInfo.FirstName} {returnedUser.EmployeeInfo.LastName}");
            var removeID = returnedUser.EmployeeInfo.Id;

            this.logger.Information("RemoveId =" + removeID);

            if (!await RemoveAssignedLockers(removeID))
            {
                return false;
            }

            var removeUser = new removeEmployee();
            removeUser.EmployeeId = removeID;
            var removeUserResponse = await client.removeEmployeeAsync(removeUser.EmployeeId);
            if (removeUserResponse.RemoveResult != null)
            {
                this.logger.Information("User was removed.");
                return true;
            }
            else
            {
                return false;
            }

        }

        public async Task<bool> RemoveEmployeebyId(long employeeId)
        {
            this.logger.Information($"Removing Employee with ID = {employeeId}");

            if (!await RemoveAssignedLockers(employeeId))
            {
                return false;
            }

            var removeUser = new removeEmployee();
            removeUser.EmployeeId = employeeId;
            var removeUserResponse = await client.removeEmployeeAsync(employeeId);

            if (removeUserResponse.RemoveResult != null)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        public async Task<long> GetBadgeIdentifierType()
        {
            this.logger.Debug("getSmartfaceBadgeIdentifierType");

            var findIdentifierType = new findIdentifierType();
            findIdentifierType.IdentifierTypeSearchInfo = new IdentifierTypeInfo();
            findIdentifierType.IdentifierTypeSearchInfo.Name = SmartFaceIdentifier;
            var getIdentifierType = await client.findIdentifierTypeAsync(findIdentifierType.IdentifierTypeSearchInfo);

            if (getIdentifierType.IdentifierTypeList.Length > 0)
            {
                return getIdentifierType.IdentifierTypeList[0].Id;
            }
            else
            {
                return 0;
            }
        }

        public async Task<long> GetFreefieldDefinitionId()
        {
            this.logger.Debug("getFreefieldDefinitionId");

            var getFreefieldId = new findFreeFieldDefinition();
            getFreefieldId.FreeFieldDefinitionSearchInfo = new FreeFieldDefinitionSearchInfo();
            getFreefieldId.FreeFieldDefinitionSearchInfo.Name = SmartFaceIdFreefield;
            var getFreefildDefId = await client.findFreeFieldDefinitionAsync(getFreefieldId.FreeFieldDefinitionSearchInfo);

            if (getFreefildDefId.FreeFieldDefinitionList.Length > 0)
            {
                return getFreefildDefId.FreeFieldDefinitionList[0].Id;
            }
            else
            {
                return 0;
            }
        }

        public async Task<EmployeeInfoComplete> GetEmployeeId(string localSmartFaceId, long localFreefieldDefId)
        {

            var employeeSearch = new EmployeeSearchInfo();
            employeeSearch.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();
            employeeSearch.EmployeeInfo.Freefield = new FreeFieldInfo[1];
            employeeSearch.EmployeeInfo.Freefield[0] = new FreeFieldInfo();
            employeeSearch.EmployeeInfo.Freefield[0].DefinitionId = localFreefieldDefId;
            employeeSearch.EmployeeInfo.Freefield[0].Name = SmartFaceIdFreefield;
            employeeSearch.EmployeeInfo.Freefield[0].value = localSmartFaceId;

            var employeesResponse = await client.findEmployeeAsync(employeeSearch);

            var foundEmployee = employeesResponse.EmployeeList
                    .FirstOrDefault(e => e.EmployeeInfo.Freefield.Any(ff => ff.Name == SmartFaceIdFreefield && ff.value == localSmartFaceId));

            return foundEmployee;

        }

        public async Task<bool> GetKeepUserStatus(long userId)
        {
            var employeeSearch = new EmployeeSearchInfo();
            employeeSearch.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();
            employeeSearch.EmployeeInfo.Id = userId;
            employeeSearch.EmployeeInfo.IdSpecified = true;

            var getKeepUserStatusResponse = await client.findEmployeeAsync(employeeSearch);

            foreach (var item in getKeepUserStatusResponse.EmployeeList[0].EmployeeInfo.Freefield)
            {
                if (item.Name == KeepUserField && item.value == "true")
                {
                    this.logger.Debug($"User has: {item.Name} with value {item.value}.");
                    return true;
                }

            }

            return false;

        }

        public async Task<bool> RemoveAssignedLockers(long userId)
        {
            this.logger.Information($"Removing Assigned Lockers");
            this.logger.Information($"userId = {userId}");

            var findLockersResponse = await client.findCarrierProfileAsync(userId);

            bool removalFailedTest = false;
            bool lockerRemovalInitiated = false;

            this.logger.Information($"findLockersResponse.ProfileResult.ToString():{findLockersResponse.ProfileResult.ToString()}");
            if (findLockersResponse.ProfileResult.AuthorisationLocker != null)
            {
                foreach (var item in findLockersResponse.ProfileResult.AuthorisationLocker.LockerAuthorisation)
                {
                    this.logger.Information($"Locker found with an ID: {item.LockerId.ToString()}");
                    lockerRemovalInitiated = true;
                    var findLockerById = new findLocker();
                    findLockerById.LockerSearchInfo = new LockerSearchInfo();
                    findLockerById.LockerSearchInfo.LockerSearch = new LockerSearch();
                    findLockerById.LockerSearchInfo.LockerSearch.Id = item.LockerId;
                    findLockerById.LockerSearchInfo.LockerSearch.IdSpecified = true;

                    var findLockerByIdReponse = await client.findLockerAsync(findLockerById.LockerSearchInfo);

                    if (findLockerByIdReponse.LockerList.Count() > 0)
                    {

                        foreach (var locker in findLockerByIdReponse.LockerList)
                        {
                            this.logger.Information($"Removing a locker-> Id: {locker.Id}, Name: {locker.Name}, Location: {locker.Location}, HostName: {locker.HostName}");

                            var removeLockerAuthorisationResponse = await client.removeCarrierLockerAuthorizationAsync(locker.Id);

                            if (removeLockerAuthorisationResponse.ProfileResult != null)
                            {
                                this.logger.Information($"The locker was successfully removed");
                            }
                            else
                            {
                                this.logger.Information($"The locker removal failed");
                                removalFailedTest = true;
                            }

                        }

                    }

                }

                if (lockerRemovalInitiated)
                {
                    this.logger.Information("Lockers removed.");
                }

                if (removalFailedTest)
                {
                    this.logger.Information($"Issue with locker removal occured.");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }

        }

        public async Task<bool> UpdateBiometricStatus(long userId, string biometricStatus)
        {

            this.logger.Debug($"getFreefieldDefinitionId for UpdateBiometricStatus;{userId},{biometricStatus},{BiometricEnrollmentStatus}");
            var getFreefieldId = new findFreeFieldDefinition();
            getFreefieldId.FreeFieldDefinitionSearchInfo = new FreeFieldDefinitionSearchInfo();
            getFreefieldId.FreeFieldDefinitionSearchInfo.Name = BiometricEnrollmentStatus;
            var getFreefieldDefId = await client.findFreeFieldDefinitionAsync(getFreefieldId.FreeFieldDefinitionSearchInfo);

            long biometricStatusFreefield = 0;

            if (getFreefieldDefId.FreeFieldDefinitionList.Length > 0)
            {
                biometricStatusFreefield = getFreefieldDefId.FreeFieldDefinitionList[0].Id;
            }
            else
            {
                this.logger.Information($"BiometricStatusFreefield was not found, current value: {biometricStatusFreefield}");
                return false;
            }
            this.logger.Debug($"biometricStatusFreefield: {biometricStatusFreefield}, userId: {userId}, BiometricEnrollmentStatus: {BiometricEnrollmentStatus}");

            var response = await this.GetEmployeeByAeosId(userId);
            if (response != null)
            {
                this.logger.Debug($"GetEmployeeByAeosId response was NOT null");
                var updateEmployee = new changeEmployee();
                updateEmployee.EmployeeChange = new EmployeeInfo();
                updateEmployee.EmployeeChange.Id = response.Id;
                updateEmployee.EmployeeChange.IdSpecified = true;
                updateEmployee.EmployeeChange.FirstName = response.FirstName;
                updateEmployee.EmployeeChange.LastName = response.LastName;
                updateEmployee.EmployeeChange.Freefield = new FreeFieldInfo[1];
                updateEmployee.EmployeeChange.Freefield[0] = new FreeFieldInfo();
                updateEmployee.EmployeeChange.Freefield[0].DefinitionId = biometricStatusFreefield;
                updateEmployee.EmployeeChange.Freefield[0].Name = BiometricEnrollmentStatus;
                updateEmployee.EmployeeChange.Freefield[0].value = biometricStatus;

                var updateEmployeeResponse = await client.changeEmployeeAsync(updateEmployee.EmployeeChange);
                if (updateEmployeeResponse == null)
                {
                    this.logger.Warning($"It was not possible to change the Biometric Enrollment Status for {response.FirstName} {response.LastName} with id {response.SmartFaceId}.");
                    return false;
                }
                else
                {
                    this.logger.Debug($"The {BiometricEnrollmentStatus} was updated to {biometricStatus}");
                    return true;
                }
            }
            else
            {
                this.logger.Debug($"GetEmployeeByAeosId response was NOT null");
                return false;
            }

        }

        public async Task<bool> UpdateBiometricStatusWithSFMember(SmartFaceMember member, string biometricStatus, SupportingData supportData)
        {

            var EmployeeByIdResponse = GetEmployeeId(member.Id, supportData.FreefieldDefinitionId);

            if (EmployeeByIdResponse?.Result?.EmployeeInfo?.Id != null)
            {
                var EmployeeById = EmployeeByIdResponse.Result.EmployeeInfo.Id;

                this.logger.Debug($"EmployeeByIdResponse: {EmployeeById}");
                var updatedBiometricStatus = await UpdateBiometricStatus(EmployeeById, biometricStatus);

                return updatedBiometricStatus;
            }
            else
            {
                return false;
            }
        }

    }
}
