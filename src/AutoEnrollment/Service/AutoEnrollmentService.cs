using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Innovatrics.SmartFace.Integrations.Shared.SmartFaceRestApiClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.AutoEnrollment.Models;

namespace SmartFace.AutoEnrollment.Service
{
    public class AutoEnrollmentService
    {
        public readonly int MaxParallelBlocks;
        public readonly int DetectorMaxFaces;
        public readonly int DetectorMinFaceSize;
        public readonly int DetectorMaxFaceSize;
        public readonly int DetectorFaceConfidence;
        public readonly int? DuplicateSearchThreshold;

        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OAuthService _oAuthService;
        private readonly string _debugOutputFolder;

        public AutoEnrollmentService(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            OAuthService oAuthService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));

            var config = configuration.GetSection("Config").Get<Config>();

            _debugOutputFolder = config?.DebugOutputFolder;
            MaxParallelBlocks = config?.MaxParallelActionBlocks ?? 4;
            DetectorMaxFaces = config?.RegisterMaxFaces ?? 3;
            DetectorMinFaceSize = config?.RegisterMinFaceSize ?? 30;
            DetectorMaxFaceSize = config?.RegisterMaxFaceSize ?? 600;
            DetectorFaceConfidence = config?.RegisterFaceConfidence ?? 450;
            DuplicateSearchThreshold = config?.DuplicateSearchThreshold;
        }

        internal async Task EnrollAsync(Notification notification, StreamConfiguration mapping)
        {
            if (DuplicateSearchThreshold > 0)
            {
                var isDuplicate = await CheckDuplicateAsync(notification, mapping, DuplicateSearchThreshold.Value);

                if (isDuplicate)
                {
                    _logger.Information("Face is possible duplicate, quit");
                    return;
                }
            }

            await RegisterAsync(notification, mapping);
        }

        public async Task RegisterAsync(Notification notification, StreamConfiguration mapping)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            _logger.Information("Enrolling new member to watchlist {Watchlist}", mapping.WatchlistIds);

            if (!(mapping.WatchlistIds?.Length > 0))
            {
                _logger.Information("No target watchlist id, skipped");
                return;
            }

            var schema = _configuration.GetValue("Target:Schema", "http");
            var host = _configuration.GetValue("Target:Host", "SFApi");
            var port = _configuration.GetValue("Target:Port", 8098);

            var baseUri = new Uri($"{schema}://{host}:{port}/");

            var httpClient = _httpClientFactory.CreateClient();

            if (_oAuthService.IsEnabled)
            {
                var authToken = await _oAuthService.GetTokenAsync();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }

            var client = new SmartFaceRestApiClient(baseUri.ToString(), httpClient);

            var registerRequest = new RegisterWatchlistMemberRequest();

            var id = Guid.NewGuid();

            registerRequest.Id = $"{id}";

            registerRequest.FullName = $"{id}";
            registerRequest.DisplayName = $"{id}";

            foreach (var w in mapping.WatchlistIds)
            {
                registerRequest.WatchlistIds.Add(w);
            }

            registerRequest.KeepAutoLearnPhotos = mapping.KeepAutoLearn ?? false;

            registerRequest.FaceDetectorConfig = new FaceDetectorConfig
            {
                MaxFaces = DetectorMaxFaces,
                MinFaceSize = DetectorMinFaceSize,
                MaxFaceSize = DetectorMaxFaceSize,
                ConfidenceThreshold = DetectorFaceConfidence
            };

            var imageAdd = new RegistrationImageData
            {
                Data = notification.CropImage
            };

            if (!string.IsNullOrEmpty(_debugOutputFolder))
            {
                await File.WriteAllBytesAsync(Path.Combine(_debugOutputFolder, $"{registerRequest.FullName}.jpg"), notification.CropImage);
            }

            registerRequest.Images.Add(imageAdd);

            try
            {
                await client.RegisterAsync(registerRequest);

                _logger.Information("Successfully enrolled WatchlistMember {watchlistMemberId}", id);
            }
            catch (ApiException ae)
            {
                _logger.Error(ae, $"Register failed. Response {ae.Response}");
                throw;
            }
        }

        private async Task EnrolExistingFaceAsync(Notification notification, StreamConfiguration mapping)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            _logger.Information("Enrolling new member to watchlist {Watchlist}", mapping.WatchlistIds);

            if (!(mapping.WatchlistIds?.Length > 0))
            {
                _logger.Information("No target watchlist id, skipped");
                return;
            }

            var schema = _configuration.GetValue("Target:Schema", "http");
            var host = _configuration.GetValue("Target:Host", "SFApi");
            var port = _configuration.GetValue("Target:Port", 8098);

            var baseUri = new Uri($"{schema}://{host}:{port}/");

            var httpClient = _httpClientFactory.CreateClient();

            if (_oAuthService.IsEnabled)
            {
                var authToken = await _oAuthService.GetTokenAsync();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }

            var client = new SmartFaceRestApiClient(baseUri.ToString(), httpClient);

            var wlMemberCreateRequest = new WatchlistMemberCreateRequest();

            var id = Guid.NewGuid();

            wlMemberCreateRequest.FullName = $"{id}";
            wlMemberCreateRequest.DisplayName = $"{id}";

            var wlMemberCreateResponse = await client.WatchlistMembersPOSTAsync(wlMemberCreateRequest);

            foreach (var watchlistId in mapping.WatchlistIds)
            {
                await client.LinkToWatchlistAsync(new WatchlistMembersLinkRequest()
                {
                    WatchlistId = watchlistId,
                    WatchlistMembersIds = new string[] { wlMemberCreateResponse.Id }
                });
            }

            if (!string.IsNullOrEmpty(_debugOutputFolder))
            {
                await File.WriteAllBytesAsync(Path.Combine(_debugOutputFolder, $"{wlMemberCreateRequest.FullName}.jpg"), notification.CropImage);
            }

            await client.AddFaceFromSystemAsync(wlMemberCreateResponse.Id, new FaceWatchlistMemberLinkingRequest()
            {
                FaceId = Guid.Parse(notification.FaceId)
            });
        }

        
        public async Task<bool> CheckDuplicateAsync(Notification notification, StreamConfiguration mapping, int threshold)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            _logger.Information("Searching for duplicate in watchlist {Watchlist}", mapping.WatchlistIds);

            var schema = _configuration.GetValue("Target:Schema", "http");
            var host = _configuration.GetValue("Target:Host", "SFApi");
            var port = _configuration.GetValue("Target:Port", 8098);

            var baseUri = new Uri($"{schema}://{host}:{port}/");

            var httpClient = _httpClientFactory.CreateClient();

            if (_oAuthService.IsEnabled)
            {
                var authToken = await _oAuthService.GetTokenAsync();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }

            var client = new SmartFaceRestApiClient(baseUri.ToString(), httpClient);

            var request = new SearchInWatchlistRequest()
            {

            };

            if (mapping.WatchlistIds.Length > 0)
            {
                request.WatchlistIds = new List<string>();

                foreach (var w in mapping.WatchlistIds)
                {
                    request.WatchlistIds.Add(w);
                }
            }

            request.FaceDetectorConfig = new FaceDetectorConfig
            {
                MaxFaces = DetectorMaxFaces,
                MinFaceSize = DetectorMinFaceSize,
                MaxFaceSize = DetectorMaxFaceSize,
                ConfidenceThreshold = DetectorFaceConfidence
            };

            request.Threshold = threshold;

            request.Image = new ImageData()
            {
                Data = notification.CropImage
            };

            try
            {
                var response = await client.SearchAllAsync(request);
                return response.SelectMany(sm => sm.MatchResults).Any();
            }
            catch (ApiException ae)
            {
                _logger.Error(ae, $"Register failed. Response {ae.Response}");
                throw;
            }
        }
    }
}
