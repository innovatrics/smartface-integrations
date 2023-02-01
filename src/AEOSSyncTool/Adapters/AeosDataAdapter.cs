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
        private int SmartFaceGraphQLPageSize;
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

           
            AEOSendpoint = configuration.GetValue<string>("aeossync:AeosServerWdsl");
            AEOSServerPageSize = configuration.GetValue<int>("aeossync:AeosServerPageSize");
            AEOSusername = configuration.GetValue<string>("aeossync:AeosServerUser");
            AEOSpassword = configuration.GetValue<string>("aeossync:AeosServerPass");
            SmartFaceIdFreefield = configuration.GetValue<string>("aeossync:AeosSmartFaceFreefield");
            SmartFaceIdentifier = configuration.GetValue<string>("aeossync:AeosSmartFaceIdentifier");


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
            this.logger.Information("Receiving Employees from AEOS");

            List<AeosMember> AeosAllMembers = new List<AeosMember>();

            // Initiate SOAP Setup END
            bool allEmployees = false;
            int EmployeesPageSize = AEOSServerPageSize; 
            int EmployeesPageNumber = 0;

            List<AeosMember> AeosAllMembersReturn = new List<AeosMember>();

            // CALL
            while(allEmployees == false)
            {
                EmployeesPageNumber += 1;
                var employeeSearch = new EmployeeSearchInfo();
                employeeSearch.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();
                //esi1.EmployeeInfo.Id = 52;
                //esi1.EmployeeInfo.IdSpecified = true;
                //esi1.EmployeeInfo.Gender = "Male";

                // because of the pagination we need to add searching information to force the pagination
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
                
                // let's put them together to a list
                foreach (var employee in employees.EmployeeList)
                {   
                    //var smartFaceId = AeosExtensions.GetFreefieldValue(employee.EmployeeInfo, "SmartFaceId");
                    //smartFaceId = employee.EmployeeInfo.GetFreefieldValue(SmartFaceIdFreefield);

                    this.logger.Debug($"{employee.EmployeeInfo.Id}\t{employee.EmployeeInfo.GetFreefieldValue(SmartFaceIdFreefield)}\t{employee.EmployeeInfo.FirstName}\t{employee.EmployeeInfo.LastName}");
                    AeosAllMembersReturn.Add(new AeosMember(employee.EmployeeInfo.Id, employee.EmployeeInfo.GetFreefieldValue(SmartFaceIdFreefield), employee.EmployeeInfo.FirstName, employee.EmployeeInfo.LastName));
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
            this.logger.Information($"Creating Employee {member.FirstName} {member.LastName} width id {member.SmartFaceId}");
            var encodedSmartFaceId = Encoding.UTF8.GetBytes(member.SmartFaceId);
            if(encodedSmartFaceId.Length > 28)
            {
                this.logger.Information($"The ID is longer than supported (28 bytes). {member.SmartFaceId} has {encodedSmartFaceId.Length} bytes");
                return false;
            }

            /*
            // addEmployee
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:sch="http://www.nedap.com/aeosws/schema">
                <soapenv:Header/>
                <soapenv:Body>
                    <sch:EmployeeAdd>
                        <sch:CarrierType>?</sch:CarrierType>
                        <sch:Freefield>
                            <sch:DefinitionId>?</sch:DefinitionId>
                            <!--Optional:-->
                            <sch:Name>?</sch:Name>
                            <sch:value>?</sch:value>
                            <!--Optional:-->
                            <sch:WarningId>?</sch:WarningId>
                        </sch:Freefield>
                        <sch:LastName>?</sch:LastName>
                        <sch:FirstName>?</sch:FirstName>
                    </sch:EmployeeAdd>
                </soapenv:Body>
            </soapenv:Envelope>       
            */
            

            var addEmployee = new addEmployee() 
            {
                EmployeeAdd = new EmployeeInfo() 
                {
                    CarrierType = "Employee",
                    Freefield = new[] 
                    {
                        new FreeFieldInfo() 
                        {
                            // TODO DEFINITION ID NEEDED
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
            if(addEmployeeResponse.EmployeeResult.Id != null)
            {   
                
            /* var addEmployee = new addEmployee();
            addEmployee.EmployeeAdd = new EmployeeInfo();
            addEmployee.EmployeeAdd.CarrierType = "Employee";
            addEmployee.EmployeeAdd.Freefield = new FreeFieldInfo[1];
            addEmployee.EmployeeAdd.Freefield[0] = new FreeFieldInfo();
            addEmployee.EmployeeAdd.Freefield[0].Name = "SmartFaceId";
            addEmployee.EmployeeAdd.Freefield[0].value = member.SmartFaceId;
            addEmployee.EmployeeAdd.FirstName = member.FirstName;
            addEmployee.EmployeeAdd.LastName = member.LastName; */

                var addIdentifier = new assignToken();
                addIdentifier.IdentifierAdd = new CarrierIdentifierData();
                addIdentifier.IdentifierAdd.CarrierId = addEmployeeResponse.EmployeeResult.Id;
                addIdentifier.IdentifierAdd.IdentifierType = badgeIdentifierType;
                addIdentifier.IdentifierAdd.BadgeNumber = member.SmartFaceId;

                var addIdentifierResponse = await client.assignTokenAsync(addIdentifier.IdentifierAdd);

                if(addIdentifierResponse.IdentifierResult.Id != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
                /*
                //  findIdentifierType
                <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:sch="http://www.nedap.com/aeosws/schema">
                <soapenv:Header/>
                <soapenv:Body>
                    <sch:IdentifierTypeSearchInfo>
                        <sch:Name>SmartFaceBadge</sch:Name>
                    </sch:IdentifierTypeSearchInfo>
                </soapenv:Body>
                </soapenv:Envelope>
                */

                /*
                // assignToken
                <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:sch="http://www.nedap.com/aeosws/schema">
                    <soapenv:Header/>
                    <soapenv:Body>
                        <sch:IdentifierAdd>
                            <sch:CarrierId>userID</sch:CarrierId>
                            <sch:IdentifierType>52</sch:IdentifierType>
                            <sch:BadgeNumber>1113222</sch:BadgeNumber>
                            <!--Optional:-->
                            
                        </sch:IdentifierAdd>
                    </soapenv:Body>
                    </soapenv:Envelope>
                */

                // addEmployeeResponse.EmployeeResult.Id;

                
            }
            else
            {
                throw new InvalidOperationException("No user was generated.");
                return false;
            }
    
        }

        public async Task updateEmployees()
        {
            this.logger.Information("Updating Employees");
        }

        public async Task removeEmployees()
        {
            this.logger.Information("Removing Employees");
        }

        public async Task<long> getBadgeIdentifierType(){
            this.logger.Information("getSmartfaceBadgeIdentifierType");

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

        public async Task<long> getFreefieldDefinitionId(){
            this.logger.Information("getFreefieldDefinitionId");
            
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
    }
}