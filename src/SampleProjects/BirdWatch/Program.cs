using System.Text;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

// This code is usable with a version as below or newer:
// SF_VERSION=v5_4.21.0
// AC_VERSION=v5_1.9.1
// SFS_VERSION=v5_1.18.0

namespace BirdWatching
{

    class Program
    {
        private GraphQLHttpClient _graphQlClient;
        private GraphQLHttpClient _graphQlClientQuery;

        // Setup the URLs, ports and the Google Chat webhook for your Google Space
        private static string serverUrl = "";
        private static string graphQlPort = "8097";
        private string restApiPort = "8098";
        private string webhookUrl = "";       

        // We can keep this as default
        private static string graphQlDir = "graphql";

        // This is the list of object types. We will use it to understand what objects are being received
        public enum GenericObjectType
        {
            Car = 3,
            Bus = 4,
            Truck = 5,
            Motorcycle = 6,
            Bicycle = 7,
            Boat = 8,
            Airplane = 9,
            Train = 10,
            Bird = 11,
            Cat = 12,
            Dog = 13,
            Horse = 14,
            Sheep = 15,
            Cow = 16,
            Bear = 17,
            Elephant = 18,
            Giraffe = 19,
            Zebra = 20,
            Suitcase = 21,
            Backpack = 22,
            Handbag = 23,
            Umbrella = 24,
            Knife = 25
        }

        // Function to initialize the GraphQLHttpClient
        public void GraphQLSubscriptionInitialize()
        {
            var graphQLOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri($"{serverUrl}:{graphQlPort}/")
                
            };
            Console.WriteLine("Subscription EndPoint: " + graphQLOptions.EndPoint);
            _graphQlClient = new GraphQLHttpClient(graphQLOptions, new NewtonsoftJsonSerializer());
        }

        // Function to send a message into Google Space
        public async Task SendMessageToGoogleSpaceAsync(string messageText, string webhookUrl)
        {
            // Create a JSON payload for the message
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
                        //Console.WriteLine("Message sent successfully."); // Optional
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

        // Function to listen to the GraphQL Subscriptions
        public async Task ListenToGraphQLSubscriptions()
        {
            var subscriptionQuery = new GraphQLRequest
            {
                // This is a query used to listen to GraphQL Subscriptions. This can be expanded as needed
                Query = @"
                subscription {
                    objectInserted {
                        id
                        imageDataId                        
                        quality
                        genericObjectType
                        size
                        objectOrderOnFrameForType
                        objectsOnFrameCountForType
                        areaOnFrame
                        cropLeftTopX
                        cropLeftTopY
                        cropRightBottomX
                        cropRightBottomY
                    }
                    }"
            };

            var extractions = new List<JObject>();
            var subscriptionExceptions = new List<Exception>();

            // Process the notifications here
            using var objectExtractedSubscription = _graphQlClient.CreateSubscriptionStream<JObject>(subscriptionQuery)
                .Subscribe(async response => { 

                    DateTime now = DateTime.Now;
                    string imageDataId;
                    var message_type = (GenericObjectType) response.Data["objectInserted"]["genericObjectType"].Value<int>();
                    var message_quality = response.Data["objectInserted"]["quality"];
                    var message_size = response.Data["objectInserted"]["size"];
                    var message_streamId = response.Data["objectInserted"]["streamId"];
                    var message_imageDataId = response.Data["objectInserted"]["imageDataId"];

                    string imageString = "";

                    if(message_imageDataId != null)
                    {
                        imageString += $"image: {serverUrl}:{restApiPort}/api/v1/Images/{message_imageDataId}";

                        Console.WriteLine($"Detected: {message_type} [size: {message_size}px; detection quality: {message_quality}] at {now.ToLocalTime()} | {imageString}", webhookUrl);

                        // Sending the information to the Google Space
                        SendMessageToGoogleSpaceAsync($"Detected: {message_type} [size: {message_size}px; detection quality: {message_quality}] at {now.ToLocalTime()} {imageString}", webhookUrl);
                    }
                    else
                    {
                        Console.WriteLine($"Error: \n{message_type} [size: {message_size}px; detection quality: {message_quality}; streamId: {message_streamId} ] at {now.ToLocalTime()} | {imageString}", webhookUrl);
                    }

                    
                }, onError: err => Console.WriteLine("Error:"+err));

                // This while loop will keep the listening until the application is closed
                while (true)
                {
                    
                }
        }

        // This is the main function of the application
        public static async Task<int> Main()
        {
            Console.WriteLine("Initializing the object detection.");
            
            var program = new Program();
            program.GraphQLSubscriptionInitialize();
            await program.ListenToGraphQLSubscriptions();
            return 0;
        }
    }
}
