using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Serilog;

using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources
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
                    noMatchResult {
                        streamId,
                        faceId,
                        trackletId,
                        cropImage,
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
                }"
            };

            var _subscriptionStream = _graphQlClient.CreateSubscriptionStream<NoMatchResultResponse>(
                _graphQLRequest, ex =>
                {
                    _logger.Error(ex, "GraphQL subscription init error");
                });

            _subscription = _subscriptionStream.Subscribe(response =>
                    {
                        _logger.Information("NoMatchResult received for stream {Stream} and tracklet {Tracklet}",
                            response.Data.NoMatchResult?.StreamId, response.Data.NoMatchResult?.TrackletId);

                        var notification = new Notification
                        {
                            StreamId = response.Data.NoMatchResult.StreamId,
                            FaceId = response.Data.NoMatchResult.FaceId,
                            TrackletId = response.Data.NoMatchResult.TrackletId,
                            CropImage = response.Data.NoMatchResult.CropImage,

                            FaceQuality = response.Data.NoMatchResult.FaceQuality,
                            TemplateQuality = response.Data.NoMatchResult.TemplateQuality,

                            FaceArea = response.Data.NoMatchResult.FaceArea,
                            FaceSize = response.Data.NoMatchResult.FaceSize,
                            FaceOrder = response.Data.NoMatchResult.FaceOrder,
                            FacesOnFrameCount = response.Data.NoMatchResult.FacesOnFrameCount,

                            Brightness = response.Data.NoMatchResult.Brightness,
                            Sharpness = response.Data.NoMatchResult.Sharpness,

                            PitchAngle = response.Data.NoMatchResult.PitchAngle,
                            RollAngle = response.Data.NoMatchResult.RollAngle,
                            YawAngle = response.Data.NoMatchResult.YawAngle,

                            OriginProcessedAt = response.Data.NoMatchResult.ProcessedAt,
                            ReceivedAt = DateTime.UtcNow
                        };

                        OnNotification?.Invoke(notification);
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