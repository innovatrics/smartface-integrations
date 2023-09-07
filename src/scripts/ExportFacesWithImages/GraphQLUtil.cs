using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Innovatrics.SmartFace.Integrations.ExportFacesWithImages.Models;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages
{
    public static class GraphQLUtil
    {
        public static async Task<Face[]> GetAllFacesWithMatches(string url, DateTime? from = null, DateTime? to = null)
        {
            var results = new List<Face>();

            int take = 1000;
            int skip = 0;
            bool allDownloaded = false;

            do
            {
                var batchResult = await GetAllFacesWithMatches(url, take, skip, from, to);

                allDownloaded = !(batchResult.Length > 0 && batchResult.Length == take);

                skip += take;

                results.AddRange(batchResult);
            }
            while (!allDownloaded);

            return results.ToArray();
        }

        private static async Task<Face[]> GetAllFacesWithMatches(string url, int take, int skip, DateTime? from, DateTime? to)
        {
            Console.WriteLine($"{nameof(GetAllFacesWithMatches)} take: {take}, skip: {skip}");

            url = SanitizeUrl(url);

            using (var graphQLClient = new GraphQLHttpClient(url, new NewtonsoftJsonSerializer()))
            {
                var graphQlRequest = new GraphQLRequest
                {
                    Query = @"
                    query GetAllFacesWithMatches(
                            $take: Int
                            $skip: Int
                            $from: DateTime!
                            $to: DateTime!
                        ) {
                            faces(
                                take: $take
                                skip: $skip
                                where: {
                                    and: [
                                        { createdAt: { gte: $from } },
                                        { createdAt: { lte: $to } }
                                    ]
                            }) {
                                items {
                                    id,
                                    createdAt,
                                    imageDataId,
                                    matchResults {
                                        watchlistMemberId,
                                        watchlistMemberFullName
                                    }
                                    tracklet {
                                            id
                                        }
                                    }
                                }
                            }",
                    OperationName = "GetAllFacesWithMatches",
                    Variables = new
                    {
                        take = take,
                        skip = skip,
                        from = from ?? DateTime.Today,
                        to = to ?? DateTime.Today.AddDays(1).AddSeconds(-1)
                    }
                };

                var facesWithMatchesResult = await graphQLClient.SendQueryAsync<FacesWithMatchesResult>(graphQlRequest);

                Console.WriteLine($"Fetched {facesWithMatchesResult.Data?.faces?.items?.Length} items");

                return facesWithMatchesResult.Data?.faces?.items;
            }
        }

        private static string SanitizeUrl(string url)
        {
            var uri = new Uri(url);
            return uri.ToString();
        }
    }
}
