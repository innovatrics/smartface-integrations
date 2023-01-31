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
        private string SmartFaceIdFreefield = "SmartFaceId";
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
                var esi1 = new EmployeeSearchInfo();
                esi1.EmployeeInfo = new EmployeeSearchInfoEmployeeInfo();
                //esi1.EmployeeInfo.Id = 52;
                //esi1.EmployeeInfo.IdSpecified = true;
                //esi1.EmployeeInfo.Gender = "Male";

                // because of the pagination we need to add searching information to force the pagination
                esi1.SearchRange = new SearchRange();
                if(EmployeesPageNumber == 1)
                {
                    esi1.SearchRange.startRecordNo = 0;
                }
                else
                {
                    esi1.SearchRange.startRecordNo = (((EmployeesPageNumber)*(EmployeesPageSize))-EmployeesPageSize);
                }
                esi1.SearchRange.nrOfRecords = EmployeesPageSize;
                esi1.SearchRange.nrOfRecordsSpecified = true;

                var employees = await client.findEmployeeAsync(esi1);
                
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

        public async Task<bool> createEmployees(string AEOSendpoint, string AEOSusername, string AEOSpassword, List<AeosMember> member)
        {
            this.logger.Information("Creating Employees");


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

            return false;
        }

        public async Task updateEmployees()
        {
            this.logger.Information("Updating Employees");
        }

        public async Task removeEmployees()
        {
            this.logger.Information("Removing Employees");
        }
    }
}