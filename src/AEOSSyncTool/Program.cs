using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.Shared.Logging;
using Innovatrics.SmartFace.Integrations.Shared.Extensions;
using Newtonsoft.Json;
using System.Collections.Generic;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using AEOSSyncTool;


namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public class Program
    {
        public const string LOG_FILE_NAME = "SmartFace.Integrations.AEOSSync.log";
        public const string JSON_CONFIG_FILE_NAME = "appsettings.json";

        public const string SmartFaceURL = "http://10.11.80.59:8098";
        public const string SmartFaceGraphQL = "http://10.11.80.59:8097/graphql";


        private static readonly HttpClient httpClientSoap = new HttpClient();

        private static async Task Main(string[] args)
        {
            //SmartFaceDataProvider smartFaceData = new SmartFaceDataProvider();
            //smartFaceData.GetWatchlistMembers(SmartFaceURL);

            Console.WriteLine("SmartFace2AEOS Sync Initiated");

            // ###
            //  1.
            // ### Get Data from SmartFace
            
            //int SmartFacePageNumber = 1;
            int SmartFacePageSize = 100;
            
            bool allMembers = false;

            List<SmartFaceMember> SmartFaceAllMembers = new List<SmartFaceMember>();

            // Dependency injection to ease initialization of GraphQL Client
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

            while(allMembers == false)
            {
                
                { // GraphQL query Example
                    var watchlistMembers123 = await graphQlClient.GetWatchlistMembers.ExecuteAsync(SmartFaceAllMembers.Count,SmartFacePageSize);
                    foreach (var wm in watchlistMembers123.Data.WatchlistMembers.Items)
                    {
                        var imageDataId = wm.Tracklet.Faces.OrderBy(f=> f.CreatedAt).FirstOrDefault(f=> f.FaceType == FaceType.Regular)?.ImageDataId;
                        Console.WriteLine($"{wm.Id}\t{imageDataId}\t{wm.DisplayName}");
                        SmartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName));
                        
                    }
                    if(watchlistMembers123.Data.WatchlistMembers.PageInfo.HasNextPage == false)
                    {
                        allMembers = true;
                    }
                        
                }
            }

          

            // Read All the WatchlistMembers, do it per pages for the case there is too many members.
           
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
            Console.WriteLine("Employees defined in SmartFace");
            foreach (var eachMember in SmartFaceAllMembers)
            {
                Console.WriteLine("Member: {0},{1},{2}", eachMember.Id, eachMember.fullName, eachMember.displayName);
                
            } 

            // ###
            //  2.
            // ### Get Data from AEOS       

            // aeosSoapUser
            // Innovatrics1

            // Initiate SOAP Setup START
            // load this information from the appsettings.json instead!

            var endpoint = new Uri("https://10.11.64.62:8443/aeosws?wsdl");
            var username = "aeosSoapUser";
            var password = "Innovatrics1";
            const string SmartFaceIdFreefield = "SmartFaceId";

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
            client.ClientCredentials.UserName.UserName = username;
            client.ClientCredentials.UserName.Password = password;

            // Initiate SOAP Setup END
            bool allEmployees = false;
            int EmployeesPageSize = 2; //because the SOAP is paginated by 1000...
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

                // because of the pagination we need to add searching information to force the pagination...
                // add here!!!
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


            // ###
            //  3.
            // ### Compare the list of users between Aeos and SmartFace

            if(SmartFaceAllMembers != null & AeosAllMembers != null)
            {
                Console.WriteLine("\nComparing Lists:");
                foreach (var SFMember in SmartFaceAllMembers)
                {
                    //Console.WriteLine($"SF Member {SFMember.fullName} with id {SFMember.Id}: {AeosAllMembers.Where(i => i.SmartFaceId == SFMember.Id).Select(i => i.SmartFaceId).FirstOrDefault()}");
                    
                    if(AeosAllMembers.Where(i => i.SmartFaceId == SFMember.Id).Select(i => i.SmartFaceId).FirstOrDefault() != null)
                    {
                        Console.WriteLine($"SF Member {SFMember.fullName} with id {SFMember.Id} HAS a copy in AEOS.");
                        
                    }
                    else
                    {
                        Console.WriteLine($"SF Member {SFMember.fullName} with id {SFMember.Id} DOES NOT have a copy in AEOS.");

                        // Create an user in AEOS matching the SmartFace Watchlist Member
                        

                    }
                     
                }

            }

            


        }

/////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////

        private static ILogger ConfigureLogger(string[] args, IConfiguration configuration)
        {
            var commonAppDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create);

            // ReSharper disable once StringLiteralTypo
            var logDir = Path.Combine(Path.Combine(commonAppDataDirPath, "Innovatrics", "SmartFace2AEOSSync"));
            logDir = configuration.GetValue<string>("Serilog:LogDirectory", logDir);            
            var logFilePath = System.IO.Path.Combine(logDir, LOG_FILE_NAME);

            var loggerConfiguration = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Destructure.ToMaximumCollectionCount(100)
                .Destructure.ToMaximumDepth(5)
                .Destructure.ToMaximumStringLength(1000)
                .WithRollingFile(logFilePath, 15, 7)
                .WithConsole();

            var logger = loggerConfiguration.CreateLogger();
            Log.Logger = logger;

            return logger;
        }

        private static IServiceCollection ConfigureServices(IServiceCollection services, ILogger logger)
        {
            services.AddHttpClient();
            
            services.AddSingleton<ILogger>(logger);
            services.AddSingleton<IAEOSSyncAdapter, AEOSSyncAdapter>();
            services.AddSingleton<IBridge, Bridge>();
            //services.AddHostedService<MainHostedService>();

            return services;
        }

        private static IConfigurationRoot ConfigureBuilder(string[] args)
        {
            return new ConfigurationBuilder()
                    .SetMainModuleBasePath()
                    .AddJsonFile(JSON_CONFIG_FILE_NAME, optional: false)
                    .AddEnvironmentVariables()
                    .AddEnvironmentVariables($"SF_INT_AEOSSync_")
                    .AddCommandLine(args)
                    .Build();
        }

        public static IHostBuilder CreateHostBuilder(string[] args, ILogger logger, IConfigurationRoot configurationRoot)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddConfiguration(configurationRoot);
                })
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services, logger);
                })
                .UseSerilog()
                .UseSystemd()
                .UseWindowsService()
            ;
        }

    }
}
