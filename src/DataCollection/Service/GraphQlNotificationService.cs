using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Serilog;
using Innovatrics.SmartFace.DataCollection.Models;

namespace Innovatrics.SmartFace.DataCollection.Services
{
    public class GraphQlNotificationService
    {
        public event Func<Notification, Task> OnNotification;

        private readonly ILogger _logger;

        private readonly string _schema;
        private readonly string _host;
        private readonly int _port;
        private readonly string _path;

        private GraphQLHttpClient _graphQlClient;

        private IDisposable _subscription;

        public GraphQlNotificationService(
            ILogger logger,
            string schema,
            string host,
            int port,
            string path
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port == 0 ? throw new ArgumentNullException(nameof(port)) : port;
            _path = path ?? throw new ArgumentNullException(nameof(path));
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

        public static Notification ConvertToNotification(MatchResultResponse response)
        {
            var notification = new Notification
            {
                StreamId = response.MatchResult?.StreamId,
                CropImage = response.MatchResult?.CropImage,
                WatchlistMemberId = response.MatchResult?.WatchlistMemberId,
                WatchlistMemberDisplayName = response.MatchResult?.WatchlistMemberDisplayName,
                WatchlistMemberFullName = response.MatchResult?.WatchlistMemberFullName,
                Score = response.MatchResult?.Score,

                ReceivedAt = DateTime.UtcNow
            };

            return notification;
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
                    matchResult(
                    ) {
                        watchlistMemberId
                        watchlistMemberDisplayName
                        watchlistMemberFullName
                        cropImage
                        score
                    }
                }"
            };

            var _subscriptionStream = _graphQlClient.CreateSubscriptionStream<MatchResultResponse>(
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
                            _logger.Information("MatchResult received for stream {Stream} and WatchlistMember {WatchlistMember} ({WatchlistMemberDisplayName})",
                                response.Data.MatchResult?.StreamId, response.Data.MatchResult?.WatchlistMemberId, (response.Data.MatchResult?.WatchlistMemberDisplayName ?? response.Data.MatchResult?.WatchlistMemberFullName));

                            var notification = ConvertToNotification(response.Data);

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