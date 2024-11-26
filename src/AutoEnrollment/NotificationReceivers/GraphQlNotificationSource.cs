using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.AutoEnrollment.Models;
using SmartFace.AutoEnrollment.Service;

namespace SmartFace.AutoEnrollment.NotificationReceivers
{
    public class GraphQlNotificationSource : INotificationSource
    {
        public event Func<Notification, Task> OnNotification;

        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly OAuthService _oAuthService;
        private GraphQLHttpClient _graphQlClient;

        private IDisposable _subscription;

        public GraphQlNotificationSource(
            ILogger logger,
            IConfiguration configuration,
            OAuthService oAuthService
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));
        }

        public async Task StartAsync()
        {
            await StartReceivingGraphQlNotifications();
        }

        public async Task StopAsync()
        {
            _logger.Information($"Stopping receiving graphQL notifications");

            await StopReceivingGraphQlNotificationsAsync();
        }

        private async Task<GraphQLHttpClient> CreateGraphQlClient()
        {
            var schema = _configuration.GetValue<string>("Source:GraphQL:Schema", "http");
            var host = _configuration.GetValue<string>("Source:GraphQL:Host", "SFGraphQL");
            var port = _configuration.GetValue<int>("Source:GraphQL:Port", 8097);
            var path = _configuration.GetValue<string>("Source:GraphQL:Path");

            var graphQlHttpClientOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri($"{schema}://{host}:{port}{NormalizePath(path)}")
            };

            if (_oAuthService.IsEnabled)
            {
                var authToken = await _oAuthService.GetTokenAsync();

                graphQlHttpClientOptions.ConfigureWebSocketConnectionInitPayload = _ => new
                {
                    authorization = $"Bearer {authToken}",
                };
            }

            _logger.Information("Subscription EndPoint {Endpoint}", graphQlHttpClientOptions.EndPoint);

            var client = new GraphQLHttpClient(graphQlHttpClientOptions, new NewtonsoftJsonSerializer());

            return client;
        }

        private async Task StartReceivingGraphQlNotifications()
        {
            _logger.Information("Start receiving GraphQL notifications");

            _graphQlClient = await CreateGraphQlClient();

            var _graphQLRequest = new GraphQLRequest
            {
                Query = @"
                subscription {
                    identificationEvent(
                        where: { 
                            identificationEventType: { in: NOT_IDENTIFIED }
                            modality: { in: FACE }
                        }
                    ) {
                        identificationEventType 
                        streamInformation {
                            streamId
                        }
                        frameInformation {
                            width
                            height
                        }
                        modality
                        faceModalityInfo {
                            faceInformation {
                                id,
                                trackletId,
                                cropImage,
                                cropCoordinates {
                                    cropLeftTopX
                                    cropLeftTopY
                                    cropLeftBottomX
                                    cropLeftBottomY        
                                    cropRightTopX
                                    cropRightTopY
                                    cropRightBottomX
                                    cropRightBottomY
                                },
                                faceArea,
                                faceSize,
                                faceOrder,
                                facesOnFrameCount,
                                faceMaskStatus,
                                faceQuality,
                                templateQuality,
                                sharpness,
                                brightness,
                                yawAngle,
                                rollAngle,
                                pitchAngle
                            }
                        }
                    }
                }"
            };

            var _subscriptionStream = _graphQlClient.CreateSubscriptionStream<IdentificationEventResponse>(
                _graphQLRequest,
                ex =>
                {
                    _logger.Error(ex, "GraphQL subscription init error");
                });

            _subscription = _subscriptionStream.Subscribe(
                    onNext: response =>
                    {
                        if (response.Data != null)
                        {
                            _logger.Information("IdentificationEvent received for stream {Stream} and tracklet {Tracklet}",
                                response.Data.IdentificationEvent?.StreamInformation?.StreamId, response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.TrackletId);

                            var notification = new Notification
                            {
                                StreamId = response.Data.IdentificationEvent?.StreamInformation?.StreamId,
                                FaceId = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.Id,
                                TrackletId = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.TrackletId,
                                CropImage = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.CropImage,

                                FrameInformation = response.Data.IdentificationEvent?.FrameInformation,
                                CropCoordinates = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.CropCoordinates,

                                FaceQuality = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.FaceQuality,
                                TemplateQuality = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.TemplateQuality,

                                FaceArea = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.FaceArea,
                                FaceSize = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.FaceSize,
                                FaceOrder = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.FaceOrder,
                                FacesOnFrameCount = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.FacesOnFrameCount,

                                Brightness = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.Brightness,
                                Sharpness = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.Sharpness,

                                PitchAngle = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.PitchAngle,
                                RollAngle = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.RollAngle,
                                YawAngle = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.YawAngle,

                                OriginProcessedAt = response.Data.IdentificationEvent?.FaceModalityInfo?.FaceInformation?.ProcessedAt,
                                ReceivedAt = DateTime.UtcNow
                            };

                            OnNotification?.Invoke(notification);
                        }
                        else if (response.Errors != null && response.Errors.Length > 0)
                        {
                            _logger.Information("{errors} errors from GraphQL received", response.Errors.Length);

                            foreach (var e in response.Errors)
                            {
                                _logger.Error("{error}", e.Message);
                            }
                        }
                    },
                    onError: err =>
                    {
                        _logger.Error(err, "GraphQL subscription runtime error");
                    }
                );

            _logger.Information("GraphQL subscription created");
        }

        private Task StopReceivingGraphQlNotificationsAsync()
        {
            _subscription?.Dispose();

            return Task.CompletedTask;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (!path.StartsWith("/"))
            {
                path = $"/{path}";
            }

            return path;
        }
    }
}