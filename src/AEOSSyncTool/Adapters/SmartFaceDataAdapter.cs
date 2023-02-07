using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using AEOSSyncTool;
using Innovatrics.SmartFace.Integrations.AEOSSync.Nswag;
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
        private int SmartFaceSetPageSize;
        private int SmartFacePageSize;
        private int SmartFaceDefaultThreshold;

        private int MaxFaces;
        private int MaxFaceSize;
        private int MinFaceSize;
        private int ConfidenceThreshold;
        private bool KeepAutoLearnPhotos;

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
            SmartFaceSetPageSize = configuration.GetValue<int>("aeossync:SmartFaceGraphQLPageSize");
            AeosWatchlistName = configuration.GetValue<string>("aeossync:AeosWatchlistName");
            SmartFacePageSize = configuration.GetValue<int>("aeossync:SmartFacePageSize");
            SmartFaceDefaultThreshold = configuration.GetValue<int>("aeossync:SmartFaceDefaultThreshold");

            KeepAutoLearnPhotos = configuration.GetValue<bool>("AEOSSync:KeepAutoLearnPhotos");

            MaxFaces = configuration.GetValue<int>("AEOSSync:FaceDetectorConfig:MaxFaces");
            MaxFaceSize = configuration.GetValue<int>("AEOSSync:FaceDetectorConfig:MaxFaceSize");
            MinFaceSize = configuration.GetValue<int>("AEOSSync:FaceDetectorConfig:MinFaceSize");
            ConfidenceThreshold = configuration.GetValue<int>("AEOSSync:FaceDetectorConfig:ConfidenceThreshold");

            if(SmartFaceURL == null)
            {
                throw new InvalidOperationException("The SmartFace URL is not read.");
            }
            if(SmartFaceGraphQL == null)
            {
                throw new InvalidOperationException("The SmartFace GraphQL URL is not read.");
            }
            if(SmartFaceSetPageSize <= 0)
            {
                throw new InvalidOperationException("The SmartFace GraphQL Page Size needs to be greater than 0.");
            }
            if(SmartFaceDefaultThreshold <= 0)
            {
                throw new InvalidOperationException("The SmartFace threshold needs to be greater than 0.");
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
                    var watchlistMembers = await graphQlClient.GetWatchlistMembers.ExecuteAsync(SmartFaceAllMembers.Count,SmartFaceSetPageSize);
                    foreach (var wm in watchlistMembers.Data.WatchlistMembers.Items)
                    {
                        var imageDataId = wm.Tracklet.Faces.OrderBy(f=> f.CreatedAt).FirstOrDefault(f=> f.FaceType == AEOSSyncTool.FaceType.Regular)?.ImageDataId;
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

        public async Task<bool> createEmployee(SmartFaceMember member, string WatchlistId)
        {
            this.logger.Information("Creating Employees");

            if(member.ImageData != null)
            {

                var httpClient = new HttpClient();   
                var restAPI = new NSwagClient(SmartFaceURL, httpClient);

                var WatchlistMemberAdd = new RegisterWatchlistMemberRequest();
                this.logger.Information($"{member.ToString()}");
                            
                WatchlistMemberAdd.Id = member.Id;
                WatchlistMemberAdd.FullName = member.FullName;
                WatchlistMemberAdd.DisplayName = member.DisplayName;
                
                this.logger.Information($"WatchlistId->{WatchlistId}");            
                WatchlistMemberAdd.WatchlistIds.Add(WatchlistId);
                WatchlistMemberAdd.KeepAutoLearnPhotos = KeepAutoLearnPhotos;
                WatchlistMemberAdd.FaceDetectorConfig = new FaceDetectorConfig();
                WatchlistMemberAdd.FaceDetectorConfig.MaxFaces = MaxFaces;
                WatchlistMemberAdd.FaceDetectorConfig.MaxFaceSize = MaxFaceSize;
                WatchlistMemberAdd.FaceDetectorConfig.MinFaceSize = MinFaceSize;
                WatchlistMemberAdd.FaceDetectorConfig.ConfidenceThreshold = ConfidenceThreshold;
                
                var imageAdd = new RegistrationImageData[1];
                imageAdd[0] = new RegistrationImageData();
                imageAdd[0].Data = member.ImageData;
                this.logger.Information($"ImageData in bytes: {imageAdd[0].Data}");
                WatchlistMemberAdd.Images.Add(imageAdd[0]);
            
                restAPI.ReadResponseAsString = true;
                var restAPIresult = await restAPI.RegisterAsync(WatchlistMemberAdd);

                if(restAPIresult.Id != null)
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
                this.logger.Information("We will not register an user without a registration image.");
            }

            return true;
        }

        public async Task updateEmployee()
        {
            this.logger.Information("Updating Employees");
        }

        public async Task<bool> removeEmployee(SmartFaceMember member)
        {
            this.logger.Information("Removing Employees");

            var httpClient = new HttpClient();   
            var restAPI = new NSwagClient(SmartFaceURL, httpClient);

            var removeEmployee = new FaceWatchlistMemberRemoveRequest();
            removeEmployee.FaceId = member.Id;
            this.logger.Information($"FaceId = {removeEmployee.FaceId}");

            // TODO
            // missing returning values... needs to be adjusted

            if(member.Id != null)
            {
                await restAPI.WatchlistMembersDELETEAsync(removeEmployee.FaceId);
            }
            /* if(restAPIresult.)
            {
                return true;
            }
            else
            {
                return false;
            }
 */

            // dummy return
            return true;
        }

        public async Task<string> initializeWatchlist()
        {
            var httpClient = new HttpClient();   
            var restAPI = new NSwagClient(SmartFaceURL, httpClient);
            var ListAllWatchlists = new List<SmartFaceWatchlist>();
            int SmartFacePageNumber = 1;
            bool allWatchlists = false;
            
            while(allWatchlists == false)
            {
                var restAPIresult = await restAPI.WatchlistsGETAsync(false,SmartFacePageNumber,SmartFaceSetPageSize,false);

                if(restAPIresult != null)
                {
                    foreach(var item in restAPIresult.Items)
                    {
                        ListAllWatchlists.Add(new SmartFaceWatchlist(item.Id, item.FullName, item.DisplayName));
                    }
                }

                if(restAPIresult.NextPage == null)
                {   
                    allWatchlists =  true;       
                }
                else
                {
                    SmartFacePageNumber += 1;
                }

            }

            var AeosWatchlistNameId = ((IEnumerable<dynamic>)ListAllWatchlists).FirstOrDefault(f=> f.FullName == AeosWatchlistName)?.Id;

            if(AeosWatchlistNameId != null)
            {
                return (string)AeosWatchlistNameId;
            }
            else
            {
                var CreateWatchlistBody = new WatchlistCreateRequest();
                CreateWatchlistBody.Threshold = SmartFaceDefaultThreshold;
                CreateWatchlistBody.DisplayName = AeosWatchlistName;
                CreateWatchlistBody.FullName = AeosWatchlistName; 
                CreateWatchlistBody.PreviewColor = "#4adf62";
                
                var restAPIresult = await restAPI.WatchlistsPOSTAsync(CreateWatchlistBody);

                return (string)restAPIresult.Id;
            }
        }
    }
}