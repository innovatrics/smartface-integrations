using System.Text;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace BirdWatching
{
    
    class Program
    {

        private GraphQLHttpClient _graphQlClient;
        private string serverUrl;
        private string graphQlPort;
        private string restApiPort;
        private string webhookUrl;

        public void GraphQLSubscriptionInitialize()
        {
            // Initialize the GraphQLHttpClient
            var graphQLOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri($"http://{serverUrl}:{graphQlPort}/")
            };

            _graphQlClient = new GraphQLHttpClient(graphQLOptions, new NewtonsoftJsonSerializer());
        }

        public async Task SendMessageToGoogleSpace(string messageText, string webhookUrl)
        {
            // Create a JSON payload for the message
            //string messageText = "Hello from C#! This is a test message.";
            var request = new {
                text = messageText
            };

            var jsonData = JsonConvert.SerializeObject(request);

            // Create an instance of HttpClient
            using (HttpClient httpClient = new HttpClient())
            {
                // Set the content type to "application/json"
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");

                // Send the POST request with the JSON payload to the webhook URL
                try
                {
                    HttpResponseMessage response = await httpClient.PostAsync(webhookUrl, new StringContent(jsonData, Encoding.UTF8, "application/json"));

                    // Check if the response was successful
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Message sent successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to send message. Status code: {response.StatusCode}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }

        }

        public async Task<Guid> GetImageDataId(Guid frameId)
        {

            // ADD THE MAGIC HERE

            /*\
            using GraphQL;
            using GraphQL.Client.Http;
            using GraphQL.Client.Serializer.SystemTextJson;

            // Create a client with your GraphQL endpoint
            var graphQlClient = new GraphQLHttpClient("https://example.com/graphql", new SystemTextJsonSerializer());

            // Create the GraphQL request
            var request = new GraphQLHttpRequest(query);

            // Send the request and receive the response
            var response = await graphQlClient.SendQueryAsync<dynamic>(request);

            if (response.Errors != null)
            {
                foreach (var error in response.Errors)
                {
                    // Handle errors
                    Console.WriteLine($"GraphQL Error: {error.Message}");
                }
            }
            else
            {
                // Access the data in the response
                var data = response.Data;
                Console.WriteLine($"Person ID: {data.person.id}");
                Console.WriteLine($"Person Name: {data.person.name}");
                Console.WriteLine($"Person Age: {data.person.age}");
            }


            */

            return frameId;
        }

        public async Task ListenToGraphQLSubscriptions()
        {
            var subscriptionQuery = new GraphQLRequest
            {
                Query = @"
                subscription {
                    objectProcessed {
                        frameInformation {
                        streamId
                        frameId

                        }
                        objectInformation {
                        id
                        type
                        quality
                        trackletId
                        size
                        objectOrderOnFrameForType
                        objectsOnFrameCountForType
                        areaOnFrame
                        cropImage
                        
                        }
                    }
                    }"
            };

            var extractions = new List<JObject>();
            var subscriptionExceptions = new List<Exception>();

            using var objectExtractedSubscription = _graphQlClient.CreateSubscriptionStream<JObject>(subscriptionQuery)
                .Subscribe(async response => { 
                    Console.WriteLine(response.Data["objectProcessed"]["objectInformation"]["type"]); 

                    DateTime now = DateTime.Now;
                    Guid imageDataId;

                    var message_type = response.Data["objectProcessed"]["objectInformation"]["type"];
                    var message_quality = response.Data["objectProcessed"]["objectInformation"]["quality"];
                    var message_size = response.Data["objectProcessed"]["objectInformation"]["size"];
                    var message_frameId = response.Data["objectProcessed"]["objectInformation"]["size"];
                    var message_streamId = response.Data["objectProcessed"]["objectInformation"]["streamId"];

                    Guid newFrameIdGuide = Guid.Parse(message_frameId.ToString());
                    // dotiahni foto z query a object position

                    string imageString = "";

                    if(message_frameId != null)
                    {
                        imageDataId = await GetImageDataId(newFrameIdGuide);
                        imageString += $"image: http://{serverUrl}:{restApiPort}/api/v1/Images/{imageDataId}";
                    }

                    SendMessageToGoogleSpace($"Detected: {message_type} [size: {message_size}px; detection quality: {message_quality}; frameId: {message_frameId}; streamId: {message_streamId} ] at {now.ToLocalTime()} {imageString}", webhookUrl);
                }, onError: err => Console.WriteLine("Error:"+err));

            while (true)
            {
                
            }
        }

        public static async Task<int> Main()
        {
            Console.WriteLine("Initializing the object detection.");
            
            var program = new Program();
            program.serverUrl = "http://sface-integ-2u";
            program.graphQlPort = "8097";
            program.restApiPort = "8098";
            program.webhookUrl = "https://chat.googleapis.com/v1/spaces/AAAADC3POn0/messages?key=AIzaSyDdI0hCZtE6vySjMm-WEfRq3CPzqKqqsHI&token=Z-nuJXJtqQ8W6GkQ59M05YZEI0EOlUTmisDUcCS7r2c";

            program.GraphQLSubscriptionInitialize();
            await program.ListenToGraphQLSubscriptions();
            return 0;
        }
    }
}
