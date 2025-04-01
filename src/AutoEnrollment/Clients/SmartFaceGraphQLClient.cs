using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

using Serilog;

namespace SmartFace.AutoEnrollment.Service.Clients
{
    public class SmartFaceGraphQLClient(
        ILogger logger,
        IConfiguration configuration,
        OAuthService oAuthService
    )
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        private readonly OAuthService _oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));

        private readonly string _schema = configuration.GetValue<string>("AeosSync:SmartFace:GraphQL:Schema") ?? throw new InvalidOperationException("The SmartFace GraphQL URL is not read.");
        private readonly string _host = configuration.GetValue<string>("AeosSync:SmartFace:GraphQL:ServerUrl") ?? throw new InvalidOperationException("The SmartFace GraphQL URL is not read.");
        private readonly int _port = configuration.GetValue<int>("AeosSync:SmartFace:GraphQL:Port");
        private readonly string _path = configuration.GetValue<string>("AeosSync:SmartFace:GraphQL:Path");

        public async Task<WatchlistMembersResponse> GetWatchlistMembersPerWatchlistAsync(string watchlistId, int skipValue, int smartFaceSetPageSize)
        {
            var graphQlClient = await CreateGraphQlClient();

            var graphQLRequest = new GraphQLRequest
            {
                Query = @"
                query ($skip: Int, $take: Int, $watchlistId: String, $olderThan: DateTime) {
                    watchlistMembers(
                        skip: $skip, 
                        take: $take,
                        where: {
                            watchlists: { all: { id: { eq: $watchlistId } } }
                            createdAt: { lte: $olderThan }
                        }
                    ) {
                        items {
                            id
                            fullName
                            createdAt
                            watchlists {
                                id
                                fullName
                            }
                        }
                    }
                }
                ",

                Variables = new
                {
                    skip = skipValue,
                    take = smartFaceSetPageSize,
                    watchlistId = watchlistId,
                    olderThan = DateTime.UtcNow.AddDays(-1)
                }
            };

            var response = await graphQlClient.SendQueryAsync<WatchlistMembersResponse>(graphQLRequest);

            _logger.Information("Watchlist members: {WatchlistMembers}", response.Data.WatchlistMembers);

            return response.Data;
        }

        public async Task<WatchlistMembersResponse> GetWatchlistMembersPerWatchlistAsync(int skipValue, int smartFaceSetPageSize, string watchlistId)
        {
            var graphQlClient = CreateGraphQlClient();

            var graphQLRequest = new GraphQLRequest
            {
                Query = @"
                query GetWatchlistMembersPerWatchlist($skip: Int, $take: Int, $watchlistId: String) {
                    watchlistMembers(
                        skip: $skip,
                        take: $take,
                        order: { id: ASC },
                        where: { watchlists: { all: { id: { eq: $watchlistId } } } }
                    ) {
                        items {
                            id
                            fullName
                            displayName
                            note
                            tracklet {
                                faces(where: { faceType: { eq: REGULAR } }) {
                                    createdAt
                                    faceType
                                    imageDataId
                                }
                            }
                        }
                        pageInfo {
                            hasNextPage
                        }
                    }
                }",

                Variables = new
                {
                    skip = skipValue,
                    take = smartFaceSetPageSize,
                    watchlistId = watchlistId
                }
            };

            var response = await graphQlClient.SendQueryAsync<WatchlistMembersResponse>(graphQLRequest);

            _logger.Information("Watchlist members: {WatchlistMembers}", response.Data.WatchlistMembers);

            return response.Data;
        }

        public async Task<FacesResponse> GetFaceByImageDataIdAsync(Guid guid)
        {
            var graphQlClient = CreateGraphQlClient();

            var graphQLRequest = new GraphQLRequest
            {
                Query = @"
                query GetFaceByImageDataId($imageDataId: UUID) {
                    faces(where: { imageDataId: { eq: $imageDataId } }) {
                        items {
                            id
                            imageDataId
                        }
                    }
                }",

                Variables = new
                {
                    imageDataId = guid
                }
            };

            var response = await graphQlClient.SendQueryAsync<FacesResponse>(graphQLRequest);

            _logger.Information("Faces: {faces}", response.Data.Faces?.Items.Length);

            return response.Data;
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