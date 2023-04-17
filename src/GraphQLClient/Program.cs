using System;
using System.Text;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

public class Program
{
    private static void Main()
    {
        var graphQLClient = new GraphQLHttpClient("http://localhost:8097/graphql", new NewtonsoftJsonSerializer());

        var matchResultRequest = new GraphQLRequest
        {
            Query = @"
                subscription test {
                    matchResult {
                        createdAt
                        watchlistFullName
                        watchlistMemberFullName
                        watchlistMemberDisplayName
                    }
                }"
        };

        var subscriptionStream = graphQLClient.CreateSubscriptionStream<MatchResultSubscriptionResult>(matchResultRequest);

        var subscription = subscriptionStream.Subscribe((Action<GraphQLResponse<MatchResultSubscriptionResult>>)(response =>
        {
            Console.WriteLine($"Match: '{response.Data.matchResult.WatchlistMemberFullName}'");
        }));

        Console.WriteLine($"Press any key to quit");
        Console.ReadKey();

        subscription.Dispose();
        graphQLClient.Dispose();
    }
}