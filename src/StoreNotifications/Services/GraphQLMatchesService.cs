using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Serilog;
using Innovatrics.SmartFace.StoreNotifications.Models;

namespace Innovatrics.SmartFace.StoreNotifications.Services
{
    public class GraphQLMatchResultsService
    {
        private readonly ILogger _logger;

        private readonly string _schema;
        private readonly string _host;
        private readonly int _port;
        private readonly string _path;
        private readonly MatchResultObserver _observer;
        private GraphQLHttpClient _graphQlClient;

        private IDisposable _subscription;

        public GraphQLMatchResultsService(
            ILogger logger,
            string schema,
            string host,
            int port,
            string path,
            MatchResultObserver observer
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port == 0 ? throw new ArgumentNullException(nameof(port)) : port;
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        }

        public void Start()
        {
            StartReceivingGraphQlNotifications();
        }

        public void Stop()
        {
            _logger.Information($"Stopping receiving graphQL notifications");

            StopReceivingGraphQlNotifications();
        }

        private GraphQLHttpClient CreateGraphQlClient()
        {
            var graphQlHttpClientOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri($"{_schema}://{_host}:{_port}{NormalizePath(_path)}")
            };

            _logger.Information("Subscription EndPoint {Endpoint}", graphQlHttpClientOptions.EndPoint);

            var client = new GraphQLHttpClient(graphQlHttpClientOptions, new NewtonsoftJsonSerializer());

            return client;
        }

        private void StartReceivingGraphQlNotifications()
        {
            _logger.Information("Start receiving GraphQL notifications");

            _graphQlClient = CreateGraphQlClient();

            var _graphQLRequest = new GraphQLRequest
            {
                Query = @"
subscription {
  matchResult {
    streamId
    frameId
    processedAt
    trackletId
    watchlistId
    watchlistMemberId
    watchlistMemberDisplayName
    faceSize
    faceOrder
    facesOnFrameCount
    faceQuality
  }
}"
            };

            var _subscriptionStream = _graphQlClient.CreateSubscriptionStream<MatchResultResponse>(
                _graphQLRequest,
                ex =>
                {
                    _logger.Error(ex, "GraphQL subscription init error");
                }
            );

            _subscription = _subscriptionStream.Subscribe(_observer);

            _logger.Information("GraphQL subscription created");
        }

        private void StopReceivingGraphQlNotifications()
        {
            _subscription?.Dispose();
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