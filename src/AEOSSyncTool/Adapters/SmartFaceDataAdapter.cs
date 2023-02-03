using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using AEOSSyncTool;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public class SmartFaceDataAdapter : ISmartFaceDataAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly SmartFaceGraphQLClient graphQlClient; 
        private string SmartFaceURL;
        private string SmartFaceGraphQL;   
        private int SmartFaceGraphQLPageSize;
        private int SmartFacePageSize;

        private string AeosWatchlistName;

        public SmartFaceDataAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            SmartFaceGraphQLClient graphQlClient
            
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.graphQlClient = graphQlClient ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.logger.Debug("SmartFaceDataAdapter Initiated");

            SmartFaceURL = configuration.GetValue<string>("aeossync:SmartFaceServer");
            SmartFaceGraphQL = configuration.GetValue<string>("aeossync:SmartFaceGraphQL");
            SmartFaceGraphQLPageSize = configuration.GetValue<int>("aeossync:SmartFaceGraphQLPageSize");
            AeosWatchlistName = configuration.GetValue<string>("aeossync:AeosWatchlistName");
            SmartFacePageSize = configuration.GetValue<int>("aeossync:SmartFacePageSize");

            if(SmartFaceURL == null)
            {
                throw new InvalidOperationException("The SmartFace URL is not read.");
            }
            if(SmartFaceGraphQL == null)
            {
                throw new InvalidOperationException("The SmartFace GraphQL URL is not read.");
            }
            if(SmartFaceGraphQLPageSize <= 0)
            {
                throw new InvalidOperationException("The SmartFace GraphQL Page Size needs to be greater than 0.");
            }

        }

        public async Task<IList <SmartFaceMember>> getEmployees()
        {
            if (SmartFaceURL is null)
            {
                throw new ArgumentNullException(nameof(SmartFaceURL));
            }

            this.logger.Debug("Receiving Employees from SmartFace");
         
            bool allMembers = false;

            var SmartFaceAllMembers = new List<SmartFaceMember>();

            while(allMembers == false)
            {
                { 
                    var watchlistMembers = await graphQlClient.GetWatchlistMembers.ExecuteAsync(SmartFaceAllMembers.Count,SmartFaceGraphQLPageSize);
                    foreach (var wm in watchlistMembers.Data.WatchlistMembers.Items)
                    {
                        var imageDataId = wm.Tracklet.Faces.OrderBy(f=> f.CreatedAt).FirstOrDefault(f=> f.FaceType == FaceType.Regular)?.ImageDataId;
                        this.logger.Debug($"{wm.Id}\t{imageDataId}\t{wm.DisplayName}");
                        SmartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName));
                        
                    }
                    if(watchlistMembers.Data.WatchlistMembers.PageInfo.HasNextPage == false)
                    {
                        allMembers = true;
                    }                        
                }
            }

            return SmartFaceAllMembers;
        }

        public async Task<bool> createEmployee(SmartFaceMember member)
        {
            this.logger.Information("Creating Employees");

            // find what find what Watchlist ID to use

            // Add user into the watchlist


/* 
            var httpClient = new HttpClient();
            var requestUrl = SmartFaceURL+"/api/v1/WatchlistMembers"+"?PageNumber="+SmartFacePageNumber+"&PageSize="+SmartFacePageSize;
            var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            var result = await httpClient.GetAsync(requestUrl);
            string resultContent = await result.Content.ReadAsStringAsync();

            //Console.WriteLine(resultContent);

            dynamic restResults = JsonConvert.DeserializeObject(resultContent);
 */
            /*
            // REST API Read All the WatchlistMembers, do it per pages for the case there are too many members.
            
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

            return true;
        }

        public async Task updateEmployee()
        {
            this.logger.Information("Updating Employees");
        }

        public async Task removeEmployee()
        {
            this.logger.Information("Removing Employees");
        }


        public async Task<string> initializeWatchlist()
        {
            // find if a watchlist exists width variable AeosWatchlistName
            var httpClient = new HttpClient();
            
            int SmartFacePageNumber = 1;
            
            // TODO Deal with pagination to cover all the data coming from the rest api

            var requestUrl = SmartFaceURL+"/api/v1/Watchlists?"+"?PageNumber="+SmartFacePageNumber+"&PageSize="+SmartFacePageSize;
            var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            var result = await httpClient.GetAsync(requestUrl);
            string resultContent = await result.Content.ReadAsStringAsync();

            //Console.WriteLine(resultContent);
            dynamic restResults = JsonConvert.DeserializeObject(resultContent);

            //Console.WriteLine((stuff.items).Count);
            //Console.WriteLine(stuff.items[0].fullName);


            var AeosWatchlistNameId = ((IEnumerable<dynamic>)restResults.items).FirstOrDefault(f=> f.fullName == AeosWatchlistName)?.id;

            // add members from the rest api call into List<member> SmartFaceAllMembers
            /* foreach (var watchlist in restResults.items)
            {
                this.logger.Information($"watchlist from data: {watchlist}");
                //Console.WriteLine($"Member: \t{person.id}\t{person.fullName}\t{person.displayName}");
                
            } */

            return (string)AeosWatchlistNameId;
            
            // if it does return the ID, if it does not create a watchlist and return ID

            //string WatchlistId = "";

            //return WatchlistId;
        }
    }
}