using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.GoogleCalendarsConnector.Models;
using GraphQL.Client;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using Newtonsoft.Json.Linq;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public class GraphQlNotificationsService : IGraphQLSubscriptionService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private GraphQLHttpClient _graphQLClient;

        public GraphQlNotificationsService(ILogger logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public event Func<StreamGroupAggregation, Task> OnStreamGroupAggregation;

        public async Task StartAsync()
        {
            _logger.Information("Starting GraphQL subscription service");

            await StartGraphQLSubscriptionAsync();
        }

        public Task StopAsync()
        {
            _logger.Information("Stopping GraphQL subscription service");

            _graphQLClient?.Dispose();

            return Task.CompletedTask;
        }

        private Task StartGraphQLSubscriptionAsync()
        {
            var graphQLConfig = _configuration.GetSection("Source:GraphQL");
            var schema = graphQLConfig["Schema"] ?? "ws";
            var host = graphQLConfig["Host"] ?? "occupancy-controller";
            var port = graphQLConfig["Port"] ?? "80";
            var path = graphQLConfig["Path"] ?? "graphql";

            var graphQLEndpoint = $"{schema}://{host}:{port}/{path}";

            _logger.Information("Connecting to GraphQL endpoint: {Endpoint}", graphQLEndpoint);

            _graphQLClient = new GraphQLHttpClient(graphQLEndpoint, new NewtonsoftJsonSerializer());

            var queryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GraphQL", "streamGroupAggregation.graphql");
            string subscriptionQuery;

            subscriptionQuery = File.ReadAllText(queryFilePath);
            _logger.Information("Loaded GraphQL subscription query from {FilePath}", queryFilePath);

            var subscriptionRequest = new GraphQLRequest
            {
                Query = subscriptionQuery
            };

            _logger.Information("Starting GraphQL subscription");

            var subscriptionStream = _graphQLClient.CreateSubscriptionStream<StreamGroupAggregationResponse>(subscriptionRequest,
                exception => _logger.Error(exception, "GraphQL subscription error"));

            subscriptionStream.Subscribe(
                async response => await HandleSubscriptionData(response),
                exception => _logger.Error(exception, "GraphQL subscription error"),
                () => _logger.Information("GraphQL subscription completed"));

            return Task.CompletedTask;
        }

        private async Task HandleSubscriptionData(GraphQLResponse<StreamGroupAggregationResponse> response)
        {
            try
            {
                if (response.Data == null)
                {
                    _logger.Warning("Received null data from GraphQL subscription");
                    return;
                }

                var streamGroupAggregation = response.Data.StreamGroupAggregation;
                if (streamGroupAggregation == null)
                {
                    _logger.Warning("No streamGroupAggregation data in response");
                    return;
                }

                await OnStreamGroupAggregation?.Invoke(streamGroupAggregation);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error handling subscription data");
            }
        }
    }

    public class StreamGroupAggregationResponse
    {
        public StreamGroupAggregation StreamGroupAggregation { get; set; }
    }
}