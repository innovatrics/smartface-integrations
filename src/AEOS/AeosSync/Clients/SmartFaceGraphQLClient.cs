using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

using Serilog;

namespace Innovatrics.SmartFace.Integrations.AeosSync.Clients
{
    public class SmartFaceGraphQLClient(
        ILogger logger,
        IConfiguration configuration
        )
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private readonly string _schema = configuration.GetValue<string>("AeosSync:SmartFace:GraphQL:ServerUrl") ?? throw new InvalidOperationException("The SmartFace GraphQL URL is not read.");
        private readonly string _host = configuration.GetValue<string>("AeosSync:SmartFace:GraphQL:ServerUrl") ?? throw new InvalidOperationException("The SmartFace GraphQL URL is not read.");
        private readonly int _port = configuration.GetValue<int>("AeosSync:SmartFace:GraphQL:Port");
        private readonly string _path = configuration.GetValue<string>("AeosSync:SmartFace:GraphQL:Path");

        public async Task<WatchlistMembersResponse> GetWatchlistMembersAsync(int skipValue, int smartFaceSetPageSize)
        {
            var graphQlClient = CreateGraphQlClient();

            var graphQLRequest = new GraphQLRequest
            {
                Query = @"
                query GetWatchlistMembers($skip: Int, $take: Int) {
                    watchlistMembers(skip: $skip, take: $take, order: { id: ASC }) {
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
                    take = smartFaceSetPageSize
                }
            };

            var response = await graphQlClient.SendQueryAsync<WatchlistMembersResponse>(graphQLRequest);

            _logger.Information("Watchlist members: {WatchlistMembers}", response.Data.WatchlistMembers);

            return response.Data;
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
    }
}