using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public class AccessControlConnectorService
    {
        public readonly int MaxParallelBlocks;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        private ActionBlock<FaceGrantedNotification> _actionBlock;

        public AccessControlConnectorService(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            OAuthService oAuthService,
            DebouncingService debouncingService,
            ValidationService validationService,
            StreamMappingService streamMappingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _streamMappingService = streamMappingService ?? throw new ArgumentNullException(nameof(streamMappingService));
            _debouncingService = debouncingService ?? throw new ArgumentNullException(nameof(debouncingService));
            _oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));

            var config = configuration.GetSection("Config").Get<Config>();

            _debugOutputFolder = config.DebugOutputFolder;
            MaxParallelBlocks = config.MaxParallelActionBlocks ?? 4;
            DetectorMaxFaces = config.RegisterMaxFaces ?? 3;
            DetectorMinFaceSize = config.RegisterMinFaceSize ?? 30;
            DetectorMaxFaceSize = config.RegisterMaxFaceSize ?? 600;
            DetectorFaceConfidence = config.RegisterFaceConfidence ?? 450;
        }

        public void Start()
        {
            _actionBlock = new ActionBlock<FaceGrantedNotification>(async notification =>
            {
                try
                {
                    var mappings = _streamMappingService.CreateMappings(notification.StreamId);

                    _logger.Debug("Found {Mappings} mappings for stream {Stream}", mappings?.Count, notification.StreamId);

                    foreach (var mapping in mappings)
                    {
                        var isValidationPassed = _validationService.Validate(notification, mapping);

                        if (isValidationPassed)
                        {
                            var isBlocked = _debouncingService.IsBlocked(notification, mapping);

                            if (isBlocked)
                            {
                                continue;
                            }

                            _debouncingService.Block(notification, mapping);

                            await EnrollAsync(notification, mapping);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process message");
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = MaxParallelBlocks
            });
        }

        public async Task StopAsync()
        {
            _actionBlock.Complete();
            await _actionBlock.Completion;
        }

        public void ProcessNotification(FaceGrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            _actionBlock.Post(notification);
        }

        private async Task EnrollAsync(FaceGrantedNotification notification, StreamMapping mapping)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            await RegisterAsync(notification, mapping);
        }

        private async Task RegisterAsync(FaceGrantedNotification notification, StreamMapping mapping)
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

            await client.RegisterAsync(registerRequest);
        }

        private async Task EnrolExistingFaceAsync(FaceGrantedNotification notification, StreamMapping mapping)
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

            await client.AddFaceFromSystemAsync(wlMemberCreateResponse.Id, new FaceWatchlistMemberLinkingRequest() {
                FaceId = Guid.Parse(notification.FaceId)
            });
        }

        internal async Task SendKeepAliveSignalAsync()
        {
            throw new NotImplementedException();
        }
    }
}
