using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using AEOSSyncTool;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public class DataOrchestrator : IDataOrchestrator
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly ISmartFaceDataAdapter AEOSSync;

        private readonly SmartFaceGraphQLClient graphQlClient;       

        public DataOrchestrator(
            ILogger logger,
            IConfiguration configuration,
            ISmartFaceDataAdapter AEOSSync,
            SmartFaceGraphQLClient graphQlClient
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.AEOSSync = AEOSSync ?? throw new ArgumentNullException(nameof(AEOSSync));
            this.graphQlClient = graphQlClient ?? throw new ArgumentNullException(nameof(graphQlClient));
            
        }

        public async Task Synchronize()
        {

            string SmartFaceURL = configuration.GetValue<string>("aeossync:SmartFaceServer");       
            string SmartFaceGraphQL = configuration.GetValue<string>("aeossync:SmartFaceGraphQL");
            string AEOSendpoint = configuration.GetValue<string>("aeossync:AeosServerWdsl");
            string AEOSusername = configuration.GetValue<string>("aeossync:AeosServerUser");
            string AEOSpassword = configuration.GetValue<string>("aeossync:AeosServerPass");

            this.logger.Information("Data Orchestrator Initalized");

            Console.WriteLine("SmartFace2AEOS Sync Initiated");

            // ###
            //  1.
            // ### Get Data from SmartFace
            
            //int SmartFacePageNumber = 1;
            int SmartFacePageSize = 100;
            
            bool allMembers = false;

            List<SmartFaceMember> SmartFaceAllMembers = new List<SmartFaceMember>();

           /*  // Dependency injection to ease initialization of GraphQL Client
            var hostBuilder = Host
                .CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddSmartFaceGraphQLClient()
                        .ConfigureHttpClient((serviceProvider, httpClient) =>
                        {
                            httpClient.BaseAddress = new Uri(SmartFaceGraphQL);
                        });
                });
            
            using var host = hostBuilder.Build();
            var graphQlClient = host.Services.GetRequiredService<SmartFaceGraphQLClient>();
 */
            while(allMembers == false)
            {
                { 
                    var watchlistMembers123 = await graphQlClient.GetWatchlistMembers.ExecuteAsync(SmartFaceAllMembers.Count,SmartFacePageSize);
                    foreach (var wm in watchlistMembers123.Data.WatchlistMembers.Items)
                    {
                        var imageDataId = wm.Tracklet.Faces.OrderBy(f=> f.CreatedAt).FirstOrDefault(f=> f.FaceType == FaceType.Regular)?.ImageDataId;
                        //Console.WriteLine($"{wm.Id}\t{imageDataId}\t{wm.DisplayName}");
                        SmartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName));
                        
                    }
                    if(watchlistMembers123.Data.WatchlistMembers.PageInfo.HasNextPage == false)
                    {
                        allMembers = true;
                    }                        
                }
            }

            // REST API Read All the WatchlistMembers, do it per pages for the case there is too many members.
           
           /*
            while(allMembers == false)
            {

                
                // lets try it with graphQL instead
                var httpClient = new HttpClient();
                var requestUrl = SmartFaceURL+"/api/v1/WatchlistMembers"+"?PageNumber="+SmartFacePageNumber+"&PageSize="+SmartFacePageSize;
                var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                var result = await httpClient.GetAsync(requestUrl);
                string resultContent = await result.Content.ReadAsStringAsync();

                //Console.WriteLine(resultContent);

                dynamic restResults = JsonConvert.DeserializeObject(resultContent);

                //Console.WriteLine((stuff.items).Count);
                //Console.WriteLine(stuff.items[0].fullName);
                

                // add members from the rest api call into List<member> SmartFaceAllMembers
                foreach (var person in restResults.items)
                {
                    //Console.WriteLine(person);
                    //Console.WriteLine($"Member: \t{person.id}\t{person.fullName}\t{person.displayName}");
                    SmartFaceAllMembers.Add(new SmartFaceMember((string)person.id,(string)person.fullName,(string)person.displayName));
                }

                // check if more iterations are needed
                if((restResults.items).Count == SmartFacePageSize)
                {
                    // lets do it again with new page and merge data from previous and current run together

                    SmartFacePageNumber += 1;
                    //Console.WriteLine("### NEW PAGE");

                }
                else
                {
                    allMembers = true;

                }
            }
            */

            // check all members
            Console.WriteLine("\nEmployees defined in SmartFace");
            foreach (var eachMember in SmartFaceAllMembers)
            {
                Console.WriteLine("Member: {0},{1},{2}", eachMember.Id, eachMember.fullName, eachMember.displayName);
                
            } 
            Console.WriteLine($"The amount of SmartFace users is {SmartFaceAllMembers.Count}");

            // ###
            //  2.
            // ### Get Data from AEOS       

            // aeosSoapUser
            // Innovatrics1

            // Initiate SOAP Setup START
            // load this information from the appsettings.json instead!

            
            const string SmartFaceIdFreefield = "SmartFaceId";
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

            var client = new AeosWebServiceTypeClient(endpointBinding, endpointAddress);
            client.ClientCredentials.ServiceCertificate.SslCertificateAuthentication = new X509ServiceCertificateAuthentication()
            {
                CertificateValidationMode = X509CertificateValidationMode.None,
                RevocationMode = X509RevocationMode.NoCheck
            };
            client.ClientCredentials.UserName.UserName = AEOSusername;
            client.ClientCredentials.UserName.Password = AEOSpassword;

            // Initiate SOAP Setup END
            bool allEmployees = false;
            int EmployeesPageSize = 100; //because the SOAP is paginated by 1000...
            int EmployeesPageNumber = 0;

            List<AeosMember> AeosAllMembers = new List<AeosMember>();

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

                    //Console.WriteLine($"{employee.EmployeeInfo.Id}\t{employee.EmployeeInfo.GetFreefieldValue(SmartFaceIdFreefield)}\t{employee.EmployeeInfo.FirstName}\t{employee.EmployeeInfo.LastName}");
                    AeosAllMembers.Add(new AeosMember(employee.EmployeeInfo.Id, employee.EmployeeInfo.GetFreefieldValue(SmartFaceIdFreefield), employee.EmployeeInfo.FirstName, employee.EmployeeInfo.LastName));
                } 

                 if(employees.EmployeeList.Length == EmployeesPageSize)
                {
                    //Console.WriteLine($"End of page {EmployeesPageNumber}. Amount of Employees found: {employees.EmployeeList.Length}. Number of results match the pagination limit. Another page will be checked.");
                }
                else
                {
                    allEmployees = true;
                    //Console.WriteLine($"End of last page {EmployeesPageNumber}. Amount of Employees found: {employees.EmployeeList.Length}.");
                    break;
                }            
                
            }

            // check all members
            Console.WriteLine("\nEmployees defined in Aeos");
            foreach (var eachMember in AeosAllMembers)
            {
                Console.WriteLine("Member: {0},{1},{2},{3}", eachMember.Id, eachMember.SmartFaceId, eachMember.FirstName, eachMember.LastName);
            }
            Console.WriteLine($"The amount of AEOS users is {AeosAllMembers.Count}");

            // ###
            //  3.
            // ### Compare the list of users between Aeos and SmartFace

            List<SmartFaceMember> EmployeesToBeAdded = new List<SmartFaceMember>();
            List<SmartFaceMember> EmployeesToBeRemoved = new List<SmartFaceMember>();
            List<SmartFaceMember> EmployeesToBeUpdated = new List<SmartFaceMember>();

            if(SmartFaceAllMembers != null & AeosAllMembers != null)
            {
                Console.WriteLine("\nComparing Lists:");
                foreach (var SFMember in SmartFaceAllMembers)
                {
                    //Console.WriteLine($"SF Member {SFMember.fullName} with id {SFMember.Id}: {AeosAllMembers.Where(i => i.SmartFaceId == SFMember.Id).Select(i => i.SmartFaceId).FirstOrDefault()}");
                    
                    if(AeosAllMembers.Where(i => i.SmartFaceId == SFMember.Id).Select(i => i.SmartFaceId).FirstOrDefault() != null)
                    {
                        Console.WriteLine($"SF Member {SFMember.fullName} with id {SFMember.Id} HAS a copy in AEOS.");
                        
                        // TODO
                        // Check if an update is actually needed, if so do the update
                        if(1==2)
                        {
                            //EmployeesToBeUpdated.Add(new SmartFaceMember(SFMember.Id,SFMember.fullName,SFMember.displayName));
                        }
                    }
                    else
                    {
                        Console.WriteLine($"SF Member {SFMember.fullName} with id {SFMember.Id} DOES NOT have a copy in AEOS.");
                        EmployeesToBeAdded.Add(new SmartFaceMember(SFMember.Id,SFMember.fullName,SFMember.displayName));
                        

                    }

                    /* if(SmartFaceAllMembers.Where(i => i.SmartFaceId == SFMember.Id).Select(i => i.SmartFaceId).FirstOrDefault() != null)
                    {
                        
                    }
                    {
                        // TODO
                        // add a logic to check if it is in AEOS but not in SF
                        Console.WriteLine($"TODO");
                        EmployeesToBeRemoved.Add(new SmartFaceMember(SFMember.Id,SFMember.fullName,SFMember.displayName));
                        
                    } */
                     
                }

            }


            // TODO ADDING USER
            Console.WriteLine($"\nThe amount of employees to be added to the AEOS: {EmployeesToBeAdded.Count}");
            // Create an user in AEOS matching the SmartFace Watchlist Member

            // Create an identifier matching the SmartFaceID and add it to the Employee


            // TODO UPDATING USER
            // Update the Employee in AEOS to match data from the SmartFace
            Console.WriteLine($"The amount of employees to be added in the AEOS: {EmployeesToBeUpdated.Count}");


            // TODO REMOVING USER
            // Remove the employees that do not exist in the SmartFace anymore so they do not exist in the AEOS anymore
            Console.WriteLine($"The amount of employees to be removed from the AEOS: {EmployeesToBeRemoved.Count}");


            await this.AEOSSync.OpenAsync();
        }
    }
}