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
            
            // SmartFaceAllMembers.Add(new SmartFaceMember("id123","fullName123","displayname123"));
         
            bool allMembers = false;

            var SmartFaceAllMembers = new List<SmartFaceMember>();

            while(allMembers == false)
            {
                { 
                    var watchlistMembers123 = await graphQlClient.GetWatchlistMembers.ExecuteAsync(SmartFaceAllMembers.Count,SmartFaceGraphQLPageSize);
                    foreach (var wm in watchlistMembers123.Data.WatchlistMembers.Items)
                    {
                        var imageDataId = wm.Tracklet.Faces.OrderBy(f=> f.CreatedAt).FirstOrDefault(f=> f.FaceType == FaceType.Regular)?.ImageDataId;
                        this.logger.Debug($"{wm.Id}\t{imageDataId}\t{wm.DisplayName}");
                        SmartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName));
                        
                    }
                    if(watchlistMembers123.Data.WatchlistMembers.PageInfo.HasNextPage == false)
                    {
                        allMembers = true;
                    }                        
                }
            }

            return SmartFaceAllMembers;
        }

        public async Task createEmployees()
        {
            this.logger.Information("Creating Employees");
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