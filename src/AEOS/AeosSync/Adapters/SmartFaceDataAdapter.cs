using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using AeosSync;
using Innovatrics.SmartFace.Integrations.AeosSync.Nswag;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Innovatrics.SmartFace.Integrations.AeosSync
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
        private Dictionary<string, bool> SmartFaceSyncedWatchlists = new();

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

            SmartFaceURL = configuration.GetValue<string>("AeosSync:SmartFace:RestApi:ServerUrl") ?? throw new InvalidOperationException("The SmartFace URL is not read.");
            SmartFaceGraphQL = configuration.GetValue<string>("AeosSync:SmartFace:GraphQL:ServerUrl") ?? throw new InvalidOperationException("The SmartFace GraphQL URL is not read.");
            SmartFaceSetPageSize = configuration.GetValue<int>("AeosSync:SmartFace:GraphQL:PageSize");
            AeosWatchlistName = configuration.GetValue<string>("AeosSync:SmartFace:Import:Watchlist") ?? throw new InvalidOperationException("The watchlist name for importing into SmartFace is necessary.");
            SmartFacePageSize = configuration.GetValue<int>("AeosSync:SmartFace:RestApi:PageSize");
            SmartFaceDefaultThreshold = configuration.GetValue<int>("AeosSync:SmartFace:Import:DefaultThreshold");
            KeepAutoLearnPhotos = configuration.GetValue<bool>("AeosSync:SmartFace:Import:KeepAutoLearnPhotos");
            MaxFaces = configuration.GetValue<int>("AeosSync:SmartFace:Import:FaceDetectorConfig:MaxFaces");
            MaxFaceSize = configuration.GetValue<int>("AeosSync:SmartFace:Import:FaceDetectorConfig:MaxFaceSize");
            MinFaceSize = configuration.GetValue<int>("AeosSync:SmartFace:Import:FaceDetectorConfig:MinFaceSize");
            ConfidenceThreshold = configuration.GetValue<int>("AeosSync:SmartFace:Import:FaceDetectorConfig:ConfidenceThreshold");
            configuration.Bind("AeosSync:SmartFace:Export:SyncedWatchlists", SmartFaceSyncedWatchlists);

            if (SmartFaceSetPageSize <= 0)
            {
                throw new InvalidOperationException("The SmartFace GraphQL Page Size needs to be greater than 0.");
            }
            if (SmartFaceDefaultThreshold <= 0)
            {
                throw new InvalidOperationException("The SmartFace threshold needs to be greater than 0.");
            }
            if (MaxFaces <= 0)
            {
                throw new InvalidOperationException("The MaxFace value should be at least 1.");
            }
            if (MaxFaceSize <= 0)
            {
                throw new InvalidOperationException("The MaxFaceSize needs to be a positive value.");
            }
            if (MinFaceSize <= 0)
            {
                throw new InvalidOperationException("The MinFaceSize needs to be a positive value.");
            }
            if (ConfidenceThreshold <= 0)
            {
                throw new InvalidOperationException("The ConfidenceThreshold needs to be a positive value.");
            }

        }

        public async Task<IList<SmartFaceMember>> GetEmployees()
        {
            if (SmartFaceURL is null)
            {
                throw new ArgumentNullException(nameof(SmartFaceURL));
            }

            this.logger.Debug("Receiving Employees from SmartFace");

            var SyncedWatchlists = new List<string>();

            if (SmartFaceSyncedWatchlists.Count > 0)
            {
                this.logger.Debug($"SmartFaceSyncedWatchlists[]: {string.Join(" ", SmartFaceSyncedWatchlists.Select(i => i.Key))}");


                foreach (var item in SmartFaceSyncedWatchlists)
                {
                    if (item.Value == true)
                    {
                        SyncedWatchlists.Add(new String(item.Key));
                    }
                }
                this.logger.Information($"SyncedWatchlists[]: {string.Join(" ", SyncedWatchlists)}");
            }
            else
            {
                this.logger.Information("SmartFaceSyncedWatchlists is empty");
            }

            var SmartFaceAllMembers = new List<SmartFaceMember>();

            if (SyncedWatchlists.Count() == 0)
            {
                bool allMembers = false;

                while (allMembers == false)
                {
                    {
                        var watchlistMembers = await graphQlClient.GetWatchlistMembers.ExecuteAsync(SmartFaceAllMembers.Count, SmartFaceSetPageSize);
                        foreach (var wm in watchlistMembers.Data.WatchlistMembers.Items)
                        {
                            var imageDataId = wm.Tracklet.Faces.OrderBy(f => f.CreatedAt).FirstOrDefault(f => f.FaceType == global::AeosSync.FaceType.Regular)?.ImageDataId;
                            this.logger.Information($"SF: {wm.Id} {imageDataId} {wm.DisplayName}");
                            SmartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName));
                        }
                        if (watchlistMembers.Data.WatchlistMembers.PageInfo.HasNextPage == false)
                        {
                            allMembers = true;
                        }
                    }
                }
            }
            else
            {
                this.logger.Information("Checking users from defined Watchlists");
                foreach (var item in SyncedWatchlists)
                {
                    this.logger.Information($"item: {item}");

                    int MemberCount = 0;
                    bool allMembers = false;
                    while (allMembers == false)
                    {
                        this.logger.Information($"MemberCount:{MemberCount},SmartFaceSetPageSize:{SmartFaceSetPageSize},item:{item}");
                        var watchlistMembers = await graphQlClient.GetWatchlistMembersPerWatchlist.ExecuteAsync(MemberCount, SmartFaceSetPageSize, item);
                        this.logger.Information($"watchlistMembers.Data.WatchlistMembers.Items.Count: {watchlistMembers.Data.WatchlistMembers.Items.Count}");
                        foreach (var wm in watchlistMembers.Data.WatchlistMembers.Items)
                        {
                            var imageDataId = wm.Tracklet.Faces.OrderBy(f => f.CreatedAt).FirstOrDefault(f => f.FaceType == global::AeosSync.FaceType.Regular)?.ImageDataId;
                            MemberCount += 1;
                            this.logger.Information($"SF: {wm.Id} {imageDataId} {wm.DisplayName} {MemberCount}");
                            SmartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName));
                        }
                        if (watchlistMembers.Data.WatchlistMembers.PageInfo.HasNextPage == false)
                        {
                            allMembers = true;
                        }
                    }
                }
            }
            return SmartFaceAllMembers;
        }

        public async Task<bool> CreateEmployee(SmartFaceMember member, string watchlistId)
        {
            this.logger.Information("Creating Employees");

            if (member.ImageData != null)
            {

                var httpClient = new HttpClient();
                var restAPI = new NSwagClient(SmartFaceURL, httpClient);

                var WatchlistMemberAdd = new RegisterWatchlistMemberRequest();
                this.logger.Information($"{member.ToString()}");

                WatchlistMemberAdd.Id = member.Id;
                WatchlistMemberAdd.FullName = member.FullName;
                WatchlistMemberAdd.DisplayName = member.DisplayName;

                this.logger.Information($"WatchlistId->{watchlistId}");
                WatchlistMemberAdd.WatchlistIds.Add(watchlistId);
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

                if (restAPIresult.Id != null)
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

        public async Task<bool> UpdateEmployee(SmartFaceMember member)
        {
            this.logger.Information("Updating Employees");

            var httpClient = new HttpClient();
            var restAPI = new NSwagClient(SmartFaceURL, httpClient);

            var updateEmployee = new WatchlistMemberUpsertRequest();
            updateEmployee.Id = member.Id;
            updateEmployee.FullName = member.FullName;
            updateEmployee.DisplayName = member.DisplayName;

            var restAPIresult = await restAPI.WatchlistMembersPUTAsync(updateEmployee);

            if (restAPIresult.Id != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> RemoveEmployee(SmartFaceMember member)
        {
            this.logger.Information("Removing Employees");

            var httpClient = new HttpClient();
            var restAPI = new NSwagClient(SmartFaceURL, httpClient);

            var removeEmployee = new FaceWatchlistMemberRemoveRequest();
            removeEmployee.FaceId = member.Id;
            this.logger.Information($"FaceId = {removeEmployee.FaceId}");

            if (member.Id != null)
            {
                var restAPIresult = await restAPI.WatchlistMembersDELETEAsync(removeEmployee.FaceId);
                return restAPIresult;
            }
            else
            {
                return false;
            }
        }

        public async Task<string> InitializeWatchlist()
        {
            var httpClient = new HttpClient();
            var restAPI = new NSwagClient(SmartFaceURL, httpClient);
            var ListAllWatchlists = new List<SmartFaceWatchlist>();
            int SmartFacePageNumber = 1;
            bool allWatchlists = false;

            while (allWatchlists == false)
            {
                var restAPIresult = await restAPI.WatchlistsGETAsync(false, SmartFacePageNumber, SmartFaceSetPageSize, false);

                if (restAPIresult != null)
                {
                    foreach (var item in restAPIresult.Items)
                    {
                        ListAllWatchlists.Add(new SmartFaceWatchlist(item.Id, item.FullName, item.DisplayName));
                    }
                }

                if (restAPIresult.NextPage == null)
                {
                    allWatchlists = true;
                }
                else
                {
                    SmartFacePageNumber += 1;
                }

            }

            var AeosWatchlistNameId = ((IEnumerable<dynamic>)ListAllWatchlists).FirstOrDefault(f => f.FullName == AeosWatchlistName)?.Id;

            if (AeosWatchlistNameId != null)
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