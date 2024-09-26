using System;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Microsoft.Extensions.Configuration;

using Serilog;

using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;
using Innovatrics.SmartFace.Integrations.Shared.SmartFaceRestApiClient;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class AutoEnrollmentService : IAutoEnrollmentService
    {
        public readonly int MAX_PARALLEL_BLOCKS;
        public readonly int DETECTOR_MAX_FACES;
        public readonly int DETECTOR_MIN_FACE_SIZE;
        public readonly int DETECTOR_MAX_FACE_SIZE;
        public readonly int DETECTOR_FACE_CONFIDENCE;

        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IValidationService validationService;
        private readonly IStreamMappingService streamMappingService;
        private readonly IDebouncingService debouncingService;
        private readonly IOAuthService _oAuthService;
        private readonly string debugOutputFolder;

        private ActionBlock<Notification> actionBlock;

        public AutoEnrollmentService(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IOAuthService oAuthService,
            IDebouncingService debouncingService,
            IValidationService validationService,
            IStreamMappingService streamMappingService
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            this.streamMappingService = streamMappingService ?? throw new ArgumentNullException(nameof(streamMappingService));
            this.debouncingService = debouncingService ?? throw new ArgumentNullException(nameof(debouncingService));
            this._oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));
            
            var config = configuration.GetSection("Config").Get<Config>();

            this.debugOutputFolder = config.DebugOutputFolder;
            MAX_PARALLEL_BLOCKS = config.MaxParallelActionBlocks ?? 4;
            DETECTOR_MAX_FACES = config.RegisterMaxFaces ?? 3;
            DETECTOR_MIN_FACE_SIZE = config.RegisterMinFaceSize ?? 30;
            DETECTOR_MAX_FACE_SIZE = config.RegisterMaxFaceSize ?? 600;
            DETECTOR_FACE_CONFIDENCE = config.RegisterFaceConfidence ?? 450;
        }

        public void Start()
        {
            this.actionBlock = new ActionBlock<Notification>(async notification =>
            {
                try
                {
                    var mappings = streamMappingService.CreateMappings(notification.StreamId);

                    this.logger.Debug("Found {mappings} mappings for stream {stream}", mappings?.Count, notification.StreamId);

                    foreach (var mapping in mappings)
                    {
                        var isValidationPassed = validationService.Validate(notification, mapping);

                        if (isValidationPassed)
                        {
                            var isBlocked = this.debouncingService.IsBlocked(notification, mapping);

                            if (isBlocked)
                            {
                                continue;
                            }

                            this.debouncingService.Block(notification, mapping);

                            await enrollAsync(notification, mapping);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logger.Error(ex, "Failed to process message");
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = MAX_PARALLEL_BLOCKS
            });
        }

        public async Task StopAsync()
        {
            actionBlock.Complete();
            await actionBlock.Completion;
        }

        public void ProcessNotification(Notification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            this.actionBlock.Post(notification);
        }

        private async Task enrollAsync(Notification notification, StreamMapping mapping)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            this.logger.Information("Enrolling new member to watchlist {watchlist}", mapping.WatchlistIds);

            if (!(mapping.WatchlistIds?.Length > 0))
            {
                this.logger.Information("No target watchlist id, skipped");
                return;
            }

            var schema = this.configuration.GetValue<string>("Target:Schema", "http");
            var host = this.configuration.GetValue<string>("Target:Host", "SFApi");
            var port = this.configuration.GetValue<int>("Target:Port", 8098);

            var baseUri = new Uri($"{schema}://{host}:{port}/");

            var httpClient = this.httpClientFactory.CreateClient();

            if (this._oAuthService.IsEnabled)
            {
                var authToken = await this._oAuthService.GetTokenAsync();
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

            registerRequest.FaceDetectorConfig = new FaceDetectorConfig();
            
            registerRequest.FaceDetectorConfig.MaxFaces = DETECTOR_MAX_FACES;
            registerRequest.FaceDetectorConfig.MinFaceSize = DETECTOR_MIN_FACE_SIZE;
            registerRequest.FaceDetectorConfig.MaxFaceSize = DETECTOR_MAX_FACE_SIZE;
            registerRequest.FaceDetectorConfig.ConfidenceThreshold = DETECTOR_FACE_CONFIDENCE;

            var imageAdd = new RegistrationImageData
            {
                Data = notification.CropImage
            };

            this.logger.Debug($"ImageData in bytes: {notification.CropImage.Length}");

            if (!string.IsNullOrEmpty(debugOutputFolder))
            {
                System.IO.File.WriteAllBytes(Path.Combine(debugOutputFolder, $"{registerRequest.FullName}.jpg"), notification.CropImage);
            }

            registerRequest.Images.Add(imageAdd);

            await client.RegisterAsync(registerRequest);
        }
    }
}
