using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.Shared.SmartFaceRestApiClient;
using Innovatrics.SmartFace.Integrations.AEOS.SmartFaceClients;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public class SmartFaceDataAdapter : ISmartFaceDataAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly SmartFaceGraphQLClient graphQlClient;
        private readonly IAeosDataAdapter aeosDataAdapter;
        private string SmartFaceURL;
        private string SmartFaceClients;
        private int SmartFaceSetPageSize;
        private int SmartFacePageSize;
        private int SmartFaceDefaultThreshold;

        private int MaxFaces;
        private int MaxFaceSize;
        private int MinFaceSize;
        private int ConfidenceThreshold;
        private bool KeepAutoLearnPhotos;
        private Dictionary<string, bool> SmartFaceSyncedWatchlists = new();
        private bool KeepPhotoUpToDate;

        private string AeosWatchlistName;

        public SmartFaceDataAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            SmartFaceGraphQLClient graphQlClient,
            IAeosDataAdapter aeosDataAdapter

        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.graphQlClient = graphQlClient ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.aeosDataAdapter = aeosDataAdapter ?? throw new ArgumentNullException(nameof(aeosDataAdapter));
            this.logger.Debug("SmartFaceDataAdapter Initiated");

            SmartFaceURL = configuration.GetValue<string>("AeosSync:SmartFace:RestApi:ServerUrl") ?? throw new InvalidOperationException("The SmartFace URL is not read.");
            SmartFaceClients = configuration.GetValue<string>("AeosSync:SmartFace:GraphQL:ServerUrl") ?? throw new InvalidOperationException("The SmartFace GraphQL URL is not read.");
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
            KeepPhotoUpToDate = configuration.GetValue<bool>("AeosSync:Aeos:KeepPhotoUpToDate");

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
                this.logger.Debug($"SyncedWatchlists[]: {string.Join(" ", SyncedWatchlists)}");
            }
            else
            {
                this.logger.Debug("SmartFaceSyncedWatchlists is empty");
            }

            var smartFaceAllMembers = new List<SmartFaceMember>();

            this.logger.Debug($"SyncedWatchlists.Count: {SyncedWatchlists.Count()}");
            if (SyncedWatchlists.Count() == 0)
            {
                bool allMembers = false;
                int skipValue = 0;
                while (allMembers == false)
                {

                    var watchlistMembers = await graphQlClient.GetWatchlistMembers.ExecuteAsync(skipValue, SmartFaceSetPageSize);
                    skipValue += SmartFaceSetPageSize;

                    foreach (var wm in watchlistMembers.Data.WatchlistMembers.Items)
                    {

                        var imageDataId = wm.Tracklet.Faces.OrderBy(f => f.CreatedAt).FirstOrDefault(f => f.FaceType == global::Innovatrics.SmartFace.Integrations.AEOS.SmartFaceClients.FaceType.Regular)?.ImageDataId;
                        if (KeepPhotoUpToDate)
                        {
                            if (imageDataId != null)
                            {

                                var imageDataBytes = await GetImageData(imageDataId.ToString());

                                if (imageDataBytes != null)
                                {

                                    this.logger.Debug($"{wm.Id}, {wm.FullName}, {wm.DisplayName}, {imageDataBytes}, {wm.Note},{imageDataId.ToString()}");
                                    smartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName, imageDataBytes, wm.Note, imageDataId.ToString()));

                                }
                                else
                                {
                                    this.logger.Debug($"{wm.Id}, {wm.FullName}, {wm.DisplayName}, {imageDataBytes}, {wm.Note},{imageDataId.ToString()}");
                                    smartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName, null, wm.Note, imageDataId.ToString()));
                                }

                            }
                        }
                        else
                        {
                            this.logger.Debug($"SF: {wm.Id} {imageDataId} {wm.DisplayName}, {wm.Note}:{imageDataId}");
                            this.logger.Debug($"Trying user {wm.Id}-{wm.FullName}:{wm.DisplayName}, {wm.Note}");

                            smartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName, null, wm.Note, imageDataId.ToString()));
                        }

                    }

                    if (watchlistMembers.Data.WatchlistMembers.PageInfo.HasNextPage == false)
                    {
                        allMembers = true;
                    }

                }
                this.logger.Debug($"Amount of member found altogether: {smartFaceAllMembers.Count()}");
            }
            else
            {
                this.logger.Debug("Checking users from defined Watchlists");
                foreach (var item in SyncedWatchlists)
                {
                    this.logger.Debug($"item: {item}");

                    int skipValue = 0;
                    bool allMembers = false;
                    while (allMembers == false)
                    {
                        this.logger.Debug($"MemberCount:{skipValue},SmartFaceSetPageSize:{SmartFaceSetPageSize},item:{item}");

                        var watchlistMembers = await graphQlClient.GetWatchlistMembersPerWatchlist.ExecuteAsync(skipValue, SmartFaceSetPageSize, item);
                        skipValue += SmartFaceSetPageSize;

                        this.logger.Debug($"watchlistMembers.Data.WatchlistMembers.Items.Count: {watchlistMembers.Data.WatchlistMembers.Items.Count}");
                        foreach (var wm in watchlistMembers.Data.WatchlistMembers.Items)
                        {
                            var imageDataId = wm.Tracklet.Faces.OrderBy(f => f.CreatedAt).FirstOrDefault(f => f.FaceType == global::Innovatrics.SmartFace.Integrations.AEOS.SmartFaceClients.FaceType.Regular)?.ImageDataId;
                            if (KeepPhotoUpToDate)
                            {
                                var imageDataBytes = await GetImageData(imageDataId.ToString());
                                if (imageDataBytes != null)
                                {
                                    smartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName, imageDataBytes, wm.Note, imageDataId.ToString()));
                                }
                                else
                                {
                                    smartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName, null, wm.Note, imageDataId.ToString()));
                                }
                            }
                            else
                            {
                                smartFaceAllMembers.Add(new SmartFaceMember(wm.Id, wm.FullName, wm.DisplayName, null, wm.Note, imageDataId.ToString()));
                            }
                        }
                        if (watchlistMembers.Data.WatchlistMembers.PageInfo.HasNextPage == false)
                        {
                            allMembers = true;
                        }
                    }
                }
            }

            var result = smartFaceAllMembers.GroupBy(m => m.Id).Select(g => g.First()).ToList();
            return result;
        }

        public async Task<bool> CreateEmployee(SmartFaceMember member, string watchlistId)
        {
            this.logger.Debug($"Adding Employee > {member.ToString()} into WatchlistId->{watchlistId}");

            if (member.ImageData != null)
            {

                var httpClient = new HttpClient();
                var restAPI = new SmartFaceRestApiClient(SmartFaceURL, httpClient);
                var WatchlistMemberAdd = new RegisterWatchlistMemberRequest();

                WatchlistMemberAdd.Id = member.Id;
                WatchlistMemberAdd.FullName = member.FullName;
                WatchlistMemberAdd.DisplayName = member.DisplayName;
                WatchlistMemberAdd.Note = member.Note;

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
                this.logger.Debug($"ImageData in bytes: {imageAdd[0].Data}");
                WatchlistMemberAdd.Images.Add(imageAdd[0]);

                restAPI.ReadResponseAsString = true;
                var restAPIexception = false;
                try
                {
                    var restAPIresult = await restAPI.RegisterAsync(WatchlistMemberAdd);

                    if (restAPIresult.Id == null)
                    {
                        this.logger.Warning($"User {member.FullName} was not registered.");

                        SupportingData SupportData = new SupportingData(await aeosDataAdapter.GetFreefieldDefinitionId(), await aeosDataAdapter.GetBadgeIdentifierType());
                        var BiometricStatusUpdated = aeosDataAdapter.UpdateBiometricStatusWithSFMember(member, "Not Enabled - registration issue", SupportData);

                        return false;
                    }

                }
                catch (ApiException e)
                {
                    var root = JObject.Parse(e.Response);
                    this.logger.Debug($"Status Code: {e.StatusCode}, {root["detail"]}");

                    SupportingData SupportData = new SupportingData(await aeosDataAdapter.GetFreefieldDefinitionId(), await aeosDataAdapter.GetBadgeIdentifierType());
                    var BiometricStatusUpdated = aeosDataAdapter.UpdateBiometricStatusWithSFMember(member, $"Not Enabled - {root["detail"]}", SupportData);

                    restAPIexception = true;
                    return false;
                }

                if (restAPIexception == false)
                {
                    return true;
                }

            }
            else
            {
                this.logger.Warning("We will not register an user without a registration image.");

                SupportingData SupportData = new SupportingData(await aeosDataAdapter.GetFreefieldDefinitionId(), await aeosDataAdapter.GetBadgeIdentifierType());
                var BiometricStatusUpdated = aeosDataAdapter.UpdateBiometricStatusWithSFMember(member, $"Not Enabled - incorrect image", SupportData);
            }

            return true;
        }

        public async Task<bool> UpdateEmployee(SmartFaceMember member, bool keepPhotoUpToDate)
        {
            this.logger.Information($"Updating Employee > {member.FullName};{member.Id}");

            var httpClient = new HttpClient();
            var restAPI = new SmartFaceRestApiClient(SmartFaceURL, httpClient);

            var updateEmployee = new WatchlistMemberUpsertRequest();
            updateEmployee.Id = member.Id;
            updateEmployee.FullName = member.FullName;
            updateEmployee.DisplayName = member.DisplayName;
            updateEmployee.Note = member.Note;

            var restAPIresult = await restAPI.WatchlistMembersPUTAsync(updateEmployee);

            var processingIssue = false;
            if (restAPIresult.Id != null)
            {
                processingIssue = true;
            }
            else
            {
                processingIssue = false;
            }

            if (keepPhotoUpToDate)
            {
                this.logger.Debug($"keepPhotoUpToDate: {keepPhotoUpToDate}");
                string watchlistmemberId = member.Id;
                this.logger.Debug($"watchlistmemberId:{watchlistmemberId};member.ImageDataId:{member.ImageDataId}");

                if (member.ImageDataId != null)
                {
                    var FaceIdData = await graphQlClient.GetFaceByImageDataId.ExecuteAsync(Guid.Parse(member.ImageDataId));

                    Guid? FaceId = null;

                    foreach (var face in FaceIdData.Data.Faces.Items)
                    {
                        if (face.Id != null)
                        {
                            this.logger.Debug($"FaceId:{face.Id}");
                            FaceId = face.Id;
                            break;
                        }
                    }

                    if (FaceId != null)
                    {
                        var removeEmployeePhoto = new FaceWatchlistMemberRemoveRequest();
                        removeEmployeePhoto.FaceId = FaceId.Value;
                        this.logger.Debug($"removeEmployeePhoto.FaceId:{removeEmployeePhoto.FaceId}, watchlistmemberId:{watchlistmemberId}");

                        try
                        {
                            await restAPI.RemoveFaceAsync(watchlistmemberId, removeEmployeePhoto);
                        }
                        catch (ApiException e)
                        {
                            this.logger.Warning($"{e.Response};{e.StatusCode};{e.Message}");
                            this.logger.Warning("It was not possible to remove the old image.");
                        }
                    }
                }
                else
                {
                    this.logger.Information("member.ImageDataId is empty. No image was removed.");
                }

                if (member.ImageData != null)
                {
                    var addEmployeePhoto = new AddNewFaceRequest();
                    var imageAdd = new RegistrationImageData[1];
                    imageAdd[0] = new RegistrationImageData();
                    imageAdd[0].Data = member.ImageData;

                    addEmployeePhoto.ImageData.Data = imageAdd[0].Data;
                    addEmployeePhoto.FaceDetectorConfig = new FaceDetectorConfig();
                    addEmployeePhoto.FaceDetectorConfig.MaxFaces = MaxFaces;
                    addEmployeePhoto.FaceDetectorConfig.ConfidenceThreshold = ConfidenceThreshold;
                    addEmployeePhoto.FaceDetectorConfig.MinFaceSize = MinFaceSize;
                    addEmployeePhoto.FaceDetectorConfig.MaxFaceSize = MaxFaceSize;

                    var restAPIresultFace = await restAPI.AddNewFaceAsync(watchlistmemberId, addEmployeePhoto);

                    if (restAPIresultFace != null)
                    {
                        this.logger.Debug($"New image ImageDataId: {restAPIresultFace.ImageDataId}");
                        processingIssue = true;
                    }
                    else
                    {
                        processingIssue = false;
                    }
                }
                else
                {
                    processingIssue = false;
                }
            }

            return processingIssue;
        }

        public async Task RemoveEmployee(SmartFaceMember member)
        {
            this.logger.Information($"{nameof(RemoveEmployee)}(id={member.Id},name={member.FullName})");

            var httpClient = new HttpClient();
            var restAPI = new SmartFaceRestApiClient(SmartFaceURL, httpClient);

            await restAPI.WatchlistMembersDELETEAsync(member.Id);
        }

        public async Task<string> InitializeWatchlist()
        {
            var httpClient = new HttpClient();
            var restAPI = new SmartFaceRestApiClient(SmartFaceURL, httpClient);
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

        public async Task<byte[]> GetImageData(string imageDataId)
        {
            var httpClient = new HttpClient();
            var restAPI = new SmartFaceRestApiClient(SmartFaceURL, httpClient);

            var guidId = Guid.Parse(imageDataId);

            var response = await restAPI.ImagesAsync(guidId, null, null);
            var memoryStream = new System.IO.MemoryStream();
            await response.Stream.CopyToAsync(memoryStream);
            byte[] byteArray = memoryStream.ToArray();

            return byteArray;

        }
    }
}