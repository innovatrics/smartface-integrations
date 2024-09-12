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

        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IValidationService validationService;
        private readonly IStreamMappingService streamMappingService;
        private readonly IDebouncingService debouncingService;
        private readonly string debugOutputFolder;

        private ActionBlock<Notification> actionBlock;

        public AutoEnrollmentService(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
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
            
            debugOutputFolder = this.configuration.GetValue<string>("Config:DebugOutputFolder");

            MAX_PARALLEL_BLOCKS = this.configuration.GetValue<int>("Config:MaxParallelActionBlocks", 4);
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

                            await enrollAsync(notification, mapping);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logger.Error(ex, "Failed to process message");
                    throw;
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

            var client = new SmartFaceRestApiClient(baseUri.ToString(), this.httpClientFactory.CreateClient());

            var registerRequest = new RegisterWatchlistMemberRequest();

            var id = Guid.NewGuid();

            registerRequest.Id = $"{id}";

            registerRequest.FullName = $"{id}";
            registerRequest.DisplayName = $"{id}";

            foreach (var w in mapping.WatchlistIds)
            {
                registerRequest.WatchlistIds.Add(w);
            }

            registerRequest.KeepAutoLearnPhotos = mapping.KeepAutoLearn;

            registerRequest.FaceDetectorConfig = new FaceDetectorConfig();
            registerRequest.FaceDetectorConfig.MaxFaces = 3;

            registerRequest.FaceDetectorConfig.MinFaceSize = 10;
            registerRequest.FaceDetectorConfig.MaxFaceSize = 600;

            registerRequest.FaceDetectorConfig.ConfidenceThreshold = 450;

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
