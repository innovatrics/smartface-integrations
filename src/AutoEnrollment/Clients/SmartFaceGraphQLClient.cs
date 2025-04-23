using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

using Serilog;
using System.Net.Http;
using System.Net.Http.Headers;

namespace SmartFace.AutoEnrollment.Service.Clients
{
    public class SmartFaceGraphQLClient(
        ILogger logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        OAuthService oAuthService
    )
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly OAuthService _oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));

        public async Task<WatchlistMembersResponse> GetWatchlistMembersPerWatchlistAsync(string watchlistId, int skipValue, int smartFaceSetPageSize, DateTime olderThan)
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
                        pageInfo {
                            hasNextPage
                        }
                    }
                }
                ",

                Variables = new
                {
                    skip = skipValue,
                    take = smartFaceSetPageSize,
                    watchlistId = watchlistId,
                    olderThan = olderThan
                }
            };

            var response = await graphQlClient.SendQueryAsync<WatchlistMembersResponse>(graphQLRequest);

            _logger.Information("Watchlist members: {WatchlistMembers}", response.Data.WatchlistMembers);

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

            var httpClient = _httpClientFactory.CreateClient();

            if (_oAuthService.IsEnabled)
            {
                var authToken = await _oAuthService.GetTokenAsync();

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            _logger.Information("Subscription EndPoint {Endpoint}", graphQlHttpClientOptions.EndPoint);

            var client = new GraphQLHttpClient(graphQlHttpClientOptions, new NewtonsoftJsonSerializer(), httpClient);

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