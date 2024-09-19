using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
        private readonly IOAuthService _oauthService;
        private GraphQLHttpClient _graphQlClient;

        private IDisposable subscription;

        public GraphQlNotificationSource(
            ILogger logger,
            IConfiguration configuration,
            IOAuthService oauthService
        )
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this._oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
        }

        public async Task StartAsync()
        {
            this._logger.Information("Start receiving graphQL notifications");

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

            var graphQLOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri($"{schema}://{host}:{port}/")
            };

            this._logger.Information("Subscription EndPoint {endpoint}", graphQLOptions.EndPoint);

            var client = new GraphQLHttpClient(graphQLOptions, new NewtonsoftJsonSerializer());

            if (this._oauthService.IsEnabled)
            {
                var token = await this._oauthService.GetTokenAsync();
                client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }

        private async Task startReceivingGraphQlNotifications()
        {
            this._logger.Information("Start receiving GraphQL notifications");

            _graphQlClient = await this.CreateGraphQlClient();

            var _graphQLRequest = new GraphQLRequest
            {
                // This is a query used to listen to GraphQL Subscriptions. This can be expanded as needed
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

            var _subscriptionStream = _graphQlClient.CreateSubscriptionStream<NoMatchResultResponse>(_graphQLRequest);

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
                        this._logger.Error(err, "GraphQL Subscription error");
                    }
                );

            this._logger.Information("GraphQL subscription created");
        }

        private Task stopReceivingGraphQlNotificationsAsync()
        {
            this.subscription?.Dispose();

            return Task.CompletedTask;
        }
    }
}