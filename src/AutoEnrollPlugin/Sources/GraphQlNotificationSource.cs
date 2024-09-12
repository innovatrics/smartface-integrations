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

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources
{
    public class GraphQlNotificationSource : INotificationSource
    {
        public event Func<Notification22, Task> OnNotification;

        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private GraphQLHttpClient _graphQlClient;
        
        private IDisposable subscription;

        public GraphQlNotificationSource(
            ILogger logger,
            IConfiguration configuration
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public Task StartAsync()
        {
            this.logger.Information("Start receiving graphQL notifications");

            this.startReceivingGraphQlNotifications();

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            this.logger.Information($"Stopping receiving graphQL notifications");

            await this.stopReceivingGraphQlNotificationsAsync();
        }

        private GraphQLHttpClient CreateGraphQlClient()
        {
            var schema = this.configuration.GetValue<string>("Source:GraphQL:Schema", "http");
            var host = this.configuration.GetValue<string>("Source:GraphQL:Host", "SFGraphQL");
            var port = this.configuration.GetValue<int>("Source:GraphQL:Port", 8097);

            var graphQLOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri($"{schema}://{host}:{port}/")
            };

            this.logger.Information("Subscription EndPoint {endpoint}", graphQLOptions.EndPoint);

            return new GraphQLHttpClient(graphQLOptions, new NewtonsoftJsonSerializer());
        }

        private void startReceivingGraphQlNotifications()
        {
            this.logger.Information("Start receiving GraphQL notifications");
            
            _graphQlClient = this.CreateGraphQlClient();

            var _graphQLRequest = new GraphQLRequest
            {
                // This is a query used to listen to GraphQL Subscriptions. This can be expanded as needed
                Query = @"
                subscription {
                    noMatchResult {
                        streamId,
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

            var _subscriptionStream = _graphQlClient.CreateSubscriptionStream<dynamic>(_graphQLRequest);

            this.subscription = _subscriptionStream.
                Subscribe(
                    async response =>
                    {
                        this.logger.Information("Success! {stream}", response.Data.noMatchResult?.streamId);

                        // DateTime now = DateTime.Now;
                        // string imageDataId;
                        // var message_type = (GenericObjectType) response.Data["objectInserted"]["genericObjectType"].Value<int>();
                        // var message_quality = response.Data["objectInserted"]["quality"];
                        // var message_size = response.Data["objectInserted"]["size"];
                        // var message_streamId = response.Data["objectInserted"]["streamId"];
                        // var message_imageDataId = response.Data["objectInserted"]["imageDataId"];

                        // string imageString = "";

                        // if(message_imageDataId != null)
                        // {
                        //     imageString += $"image: {serverUrl}:{restApiPort}/api/v1/Images/{message_imageDataId}";

                        //     Console.WriteLine($"Detected: {message_type} [size: {message_size}px; detection quality: {message_quality}] at {now.ToLocalTime()} | {imageString}", webhookUrl);

                        //     // Sending the information to the Google Space
                        //     SendMessageToGoogleSpaceAsync($"Detected: {message_type} [size: {message_size}px; detection quality: {message_quality}] at {now.ToLocalTime()} {imageString}", webhookUrl);
                        // }
                        // else
                        // {
                        //     Console.WriteLine($"Error: \n{message_type} [size: {message_size}px; detection quality: {message_quality}; streamId: {message_streamId} ] at {now.ToLocalTime()} | {imageString}", webhookUrl);
                        // }


                    },
                    onError: err =>
                    {
                        Console.WriteLine("Error:" + err);
                    }
                );

            this.logger.Information("GraphQL subscription created");
        }

        private Task stopReceivingGraphQlNotificationsAsync()
        {
            this.subscription?.Dispose();
            
            return Task.CompletedTask;
        }
    }
}