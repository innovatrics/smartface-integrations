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
        private readonly IOAuthService _oAuthService;
        private GraphQLHttpClient _graphQlClient;

        private IDisposable subscription;

        public GraphQlNotificationSource(
            ILogger logger,
            IConfiguration configuration,
            IOAuthService oAuthService
        )
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this._oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));
        }

        public async Task StartAsync()
        {
            await this.startReceivingGraphQlNotifications();
        }

        public async Task StopAsync()
        {
            this._logger.Information($"Stopping receiving graphQL notifications");

            await this.stopReceivingGraphQlNotificationsAsync();
        }

        private async Task<GraphQLHttpClient> CreateGraphQlClient()
        {
            var schema = this._configuration.GetValue<string>("Source:GraphQL:Schema", "http");
            var host = this._configuration.GetValue<string>("Source:GraphQL:Host", "SFGraphQL");
            var port = this._configuration.GetValue<int>("Source:GraphQL:Port", 8097);
            var path = this._configuration.GetValue<string>("Source:GraphQL:Path");

            var graphQLOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri($"{schema}://{host}:{port}{normalizePath(path)}")
            };

            if (this._oAuthService.IsEnabled)
            {
                var authToken = await this._oAuthService.GetTokenAsync();

                graphQLOptions.ConfigureWebSocketConnectionInitPayload = (GraphQLHttpClientOptions opts) =>
                    {
                        return new
                        {
                            authorization = $"Bearer {authToken}",
                        };
                    };
            }

            this._logger.Information("Subscription EndPoint {endpoint}", graphQLOptions.EndPoint);

            var client = new GraphQLHttpClient(graphQLOptions, new NewtonsoftJsonSerializer());

            return client;
        }

        private async Task startReceivingGraphQlNotifications()
        {
            this._logger.Information("Start receiving GraphQL notifications");

            _graphQlClient = await this.CreateGraphQlClient();

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
                _graphQLRequest,
                (Exception e) =>
                {
                    this._logger.Error(e, "GraphQL subscription init error");
                });

            this.subscription = _subscriptionStream.
                Subscribe(
                    response =>
                    {
                        this._logger.Information("NoMatchResult received for stream {stream} and tracklet {tracklet}", response.Data.NoMatchResult?.StreamId, response.Data.NoMatchResult?.TrackletId);

                        var notification = new Notification()
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

                        this.OnNotification?.Invoke(notification);
                    },
                    onError: err =>
                    {
                        this._logger.Error(err, "GraphQL subscription runtime error");
                    }
                );

            this._logger.Information("GraphQL subscription created");
        }

        private Task stopReceivingGraphQlNotificationsAsync()
        {
            this.subscription?.Dispose();

            return Task.CompletedTask;
        }

        private string normalizePath(string path)
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