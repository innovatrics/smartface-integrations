using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;
using AEOSSyncTool;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public class AeosDataAdapter : IAeosDataAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        
        private string AEOSendpoint;
        private int AEOSServerPageSize;
        private string AEOSusername;
        private string AEOSpassword;
        private string SmartFaceIdFreefield;
        private string SmartFaceIdentifier;

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

            AEOSendpoint = configuration.GetValue<string>("aeossync:Aeos:Server:Wdsl");
            AEOSServerPageSize = configuration.GetValue<int>("aeossync:Aeos:Server:PageSize");
            AEOSusername = configuration.GetValue<string>("aeossync:Aeos:Server:User");
            AEOSpassword = configuration.GetValue<string>("aeossync:Aeos:Server:Pass");
            SmartFaceIdFreefield = configuration.GetValue<string>("aeossync:Aeos:Integration:Freefield");
            SmartFaceIdentifier = configuration.GetValue<string>("aeossync:Aeos:Integration:Identifier");

            if(AEOSendpoint == null)
            {
                throw new InvalidOperationException("The AEOS SOAP API URL is not read.");
            }
            if(AEOSServerPageSize <= 0)
            {
                throw new InvalidOperationException("The SmartFace GraphQL Page Size needs to be greater than 0.");
            }
            if(AEOSusername == null)
            {
                throw new InvalidOperationException("The AEOS username is not read.");
            }
            if(AEOSpassword == null)
            {
                throw new InvalidOperationException("The AEOS password is not read.");
            }
           
            if(SmartFaceIdFreefield == null)
            {
                throw new InvalidOperationException("The AEOS SmartFaceIdFreefield is not read.");
            }
            if(SmartFaceIdentifier == null)
            {
                throw new InvalidOperationException("The AEOS SmartFaceIdentifier is not read.");
            }

            
            var endpoint = new Uri(AEOSendpoint);
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
            client.ClientCredentials.UserName.UserName = AEOSusername;
            client.ClientCredentials.UserName.Password = AEOSpassword;
        }

        public async Task<IList <AeosMember>> getEmployees()
        {
            this.logger.Debug("Receiving Employees from AEOS");

            List<AeosMember> AeosAllMembers = new List<AeosMember>();

            bool allEmployees = false;
            int EmployeesPageSize = AEOSServerPageSize; 
            int EmployeesPageNumber = 0;

            List<AeosMember> AeosAllMembersReturn = new List<AeosMember>();

            while(allEmployees == false)
            {
                EmployeesPageNumber += 1;
                var employeeSearch = new EmployeeSearchInfo();
                employeeSearch.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();
                
                employeeSearch.SearchRange = new SearchRange();
                if(EmployeesPageNumber == 1)
                {
                    employeeSearch.SearchRange.startRecordNo = 0;
                }
                else
                {
                    employeeSearch.SearchRange.startRecordNo = (((EmployeesPageNumber)*(EmployeesPageSize))-EmployeesPageSize);
                }
                employeeSearch.SearchRange.nrOfRecords = EmployeesPageSize;
                employeeSearch.SearchRange.nrOfRecordsSpecified = true;

                var employees = await client.findEmployeeAsync(employeeSearch);
                
                foreach (var employee in employees.EmployeeList)
                {   
                    if(employee.FirstPhoto != null)
                    {
                        AeosAllMembersReturn.Add(new AeosMember(employee.EmployeeInfo.Id, employee.EmployeeInfo.GetFreefieldValue(SmartFaceIdFreefield), employee.EmployeeInfo.FirstName, employee.EmployeeInfo.LastName,employee.FirstPhoto.Picture));
                    }
                    else
                    {
                        AeosAllMembersReturn.Add(new AeosMember(employee.EmployeeInfo.Id, employee.EmployeeInfo.GetFreefieldValue(SmartFaceIdFreefield), employee.EmployeeInfo.FirstName, employee.EmployeeInfo.LastName));
                    }
                } 
                
                if(employees.EmployeeList.Length == EmployeesPageSize)
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

        public async Task<bool> createEmployees(AeosMember aeosMember, long badgeIdentifierType, long FreefieldDefinitionId)
        {

            var member = aeosMember;
            this.logger.Debug($"Creating Employee {member.FirstName} {member.LastName} width id {member.SmartFaceId}");
            var encodedSmartFaceId = Encoding.UTF8.GetBytes(member.SmartFaceId);
            if(encodedSmartFaceId.Length > 28)
            {
                this.logger.Debug($"The ID is longer than supported (28 bytes). {member.SmartFaceId} has {encodedSmartFaceId.Length} bytes");
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
                            DefinitionId = FreefieldDefinitionId,
                            Name = SmartFaceIdFreefield,
                            value = member.SmartFaceId
                        }
                    },
                    FirstName = member.FirstName,
                    LastName = member.LastName
                }
            };

            var addEmployeeResponse = await client.addEmployeeAsync(addEmployee.EmployeeAdd);
            if(addEmployeeResponse.EmployeeResult.Id != 0)
            {   

                var addIdentifier = new assignToken();
                addIdentifier.IdentifierAdd = new CarrierIdentifierData();
                addIdentifier.IdentifierAdd.CarrierId = addEmployeeResponse.EmployeeResult.Id;
                addIdentifier.IdentifierAdd.IdentifierType = badgeIdentifierType;
                addIdentifier.IdentifierAdd.BadgeNumber = member.SmartFaceId;

                var addIdentifierResponse = await client.assignTokenAsync(addIdentifier.IdentifierAdd);

                return addIdentifierResponse.IdentifierResult.Id != 0 ? true : false;
            }
            else
            {
                throw new InvalidOperationException("No user was generated.");
            }
        }

        public async Task<bool> updateEmployee(AeosMember member, long FreefieldDefinitionId)
        {
            this.logger.Information($"Updating Employee with ID = {member.SmartFaceId}, new name: {member.FirstName} {member.LastName}");

            findEmployeeResponse returnedUser = await getEmployeeId(member.SmartFaceId, FreefieldDefinitionId);

            if(returnedUser != null)
            {
                this.logger.Information($"Found a user with this SmartFaceId: {member.SmartFaceId}: {returnedUser.EmployeeList[0].EmployeeInfo.Id} {returnedUser.EmployeeList[0].EmployeeInfo.FirstName} {returnedUser.EmployeeList[0].EmployeeInfo.LastName}");
                var updateID = returnedUser.EmployeeList[0].EmployeeInfo.Id;

                // update the user under the AeosID

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

                if(updateEmployeeResponse.EmployeeResult.Id != 0)
                {
                    this.logger.Information($"Update \tUser with SmartFaceID {member.SmartFaceId} has been updated under {updateID} with new name {member.FirstName} {member.LastName}");
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

        public async Task<bool> removeEmployee(AeosMember member, long FreefieldDefinitionId)
        {
            this.logger.Debug("Removing Employee");
            this.logger.Debug($"Updating Employee with ID = {member.SmartFaceId}, new name: {member.FirstName} {member.LastName}");
            findEmployeeResponse returnedUser = await getEmployeeId(member.SmartFaceId, FreefieldDefinitionId);

            if(returnedUser != null)
            {
                this.logger.Information($"DELETE \tFound a user with this SmartFaceId: {member.SmartFaceId}: {returnedUser.EmployeeList[0].EmployeeInfo.Id} {returnedUser.EmployeeList[0].EmployeeInfo.FirstName} {returnedUser.EmployeeList[0].EmployeeInfo.LastName}");
                var removeID = returnedUser.EmployeeList[0].EmployeeInfo.Id;

                var removeUser = new removeEmployee();
                removeUser.EmployeeId = removeID;
                var removeUserResponse = await client.removeEmployeeAsync(removeUser.EmployeeId);
                if(removeUserResponse.RemoveResult != null)
                {
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

        public async Task<long> getBadgeIdentifierType(){
            this.logger.Debug("getSmartfaceBadgeIdentifierType");

            var findIdentifierType = new findIdentifierType();
            findIdentifierType.IdentifierTypeSearchInfo = new IdentifierTypeInfo();
            findIdentifierType.IdentifierTypeSearchInfo.Name = SmartFaceIdentifier;
            var getIdentifierType = await client.findIdentifierTypeAsync(findIdentifierType.IdentifierTypeSearchInfo);
            
            if(getIdentifierType.IdentifierTypeList.Length > 0)
            {
                return getIdentifierType.IdentifierTypeList[0].Id;
            }
            else
            {
                return 0;
            }
        }

        public async Task<long> getFreefieldDefinitionId()
        {
            this.logger.Debug("getFreefieldDefinitionId");
            
            var getFreefieldId = new findFreeFieldDefinition();
            getFreefieldId.FreeFieldDefinitionSearchInfo = new FreeFieldDefinitionSearchInfo();
            getFreefieldId.FreeFieldDefinitionSearchInfo.Name = SmartFaceIdFreefield;
            var getFreefildDefId = await client.findFreeFieldDefinitionAsync(getFreefieldId.FreeFieldDefinitionSearchInfo);

            if(getFreefildDefId.FreeFieldDefinitionList.Length > 0)
            {
                return getFreefildDefId.FreeFieldDefinitionList[0].Id;
            }
            else
            {
                return 0;
            }
        }

        public async Task<findEmployeeResponse> getEmployeeId(string localSmartFaceId, long localFreefieldDefId)
        {
            this.logger.Information("getEmployeeId");

            var employeeSearch = new EmployeeSearchInfo();
            employeeSearch.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();
            employeeSearch.EmployeeInfo.Freefield = new FreeFieldInfo[1];
            employeeSearch.EmployeeInfo.Freefield[0] = new FreeFieldInfo();
            employeeSearch.EmployeeInfo.Freefield[0].DefinitionId = localFreefieldDefId;
            employeeSearch.EmployeeInfo.Freefield[0].Name = SmartFaceIdFreefield;
            employeeSearch.EmployeeInfo.Freefield[0].value = localSmartFaceId;

            this.logger.Information("getEmployeeId1");
            var employeesResponse = await client.findEmployeeAsync(employeeSearch);

            this.logger.Information("getEmployeeId2");

            if(employeesResponse.EmployeeList.Length > 1)
            {
                this.logger.Error("Two or more users have the same Identifier! This should not occur.");
                throw new InvalidCastException("Two or more users have the same Identifier! This should not occur.");
            }
            else if(employeesResponse.EmployeeList.Length == 1 && employeesResponse.EmployeeList[0].EmployeeInfo.Id != 0)
            {
                return employeesResponse;
            }
            else
            {
                return null;
            }


        }

    }
}
