using System.Text;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

// use at least version
// SF_VERSION=v5_4.21.0
// AC_VERSION=v5_1.9.1
// SFS_VERSION=v5_1.18.0



namespace BirdWatching
{

    class Program
    {
        private GraphQLHttpClient _graphQlClient;
        private GraphQLHttpClient _graphQlClientQuery;
        private static string serverUrl = "http://sface-integ-2u";
        private static string graphQlPort = "8097";
        private static string graphQlDir = "graphql";
        private string restApiPort = "8098";
        private string webhookUrl = "https://chat.googleapis.com/v1/spaces/AAAADC3POn0/messages?key=AIzaSyDdI0hCZtE6vySjMm-WEfRq3CPzqKqqsHI&token=Z-nuJXJtqQ8W6GkQ59M05YZEI0EOlUTmisDUcCS7r2c";       

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

        public void GraphQLSubscriptionInitialize()
        {
            // Initialize the GraphQLHttpClient
            var graphQLOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri($"{serverUrl}:{graphQlPort}/")
                
            };
            Console.WriteLine("Subscription EndPoint: " + graphQLOptions.EndPoint);

            _graphQlClient = new GraphQLHttpClient(graphQLOptions, new NewtonsoftJsonSerializer());
        }

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

        public async Task ListenToGraphQLSubscriptions()
        {
            var subscriptionQuery = new GraphQLRequest
            {
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

            using var objectExtractedSubscription = _graphQlClient.CreateSubscriptionStream<JObject>(subscriptionQuery)
                .Subscribe(async response => { 
                    Console.WriteLine(response.Data["objectInserted"]["genericObjectType"]); 

                    DateTime now = DateTime.Now;
                    string imageDataId;

                    var message_type = response.Data["objectInserted"]["genericObjectType"];
                    var message_quality = response.Data["objectInserted"]["quality"];
                    var message_size = response.Data["objectInserted"]["size"];
                    var message_streamId = response.Data["objectInserted"]["streamId"];
                    var message_imageDataId = response.Data["objectInserted"]["imageDataId"];

                    Console.WriteLine(message_imageDataId);

                    // chyba tu
                    string objectTypeString = Enum.GetName(typeof(GenericObjectType), message_type);
                    
                    
                    Console.WriteLine(objectTypeString);
                    string imageString = "";

                    if(message_imageDataId != null)
                    {
                        imageString += $"image: {serverUrl}:{restApiPort}/api/v1/Images/{message_imageDataId}";

                        Console.WriteLine($"Detected: {message_type} [size: {message_size}px; detection quality: {message_quality}; streamId: {message_streamId} ] at {now.ToLocalTime()} | {imageString}", webhookUrl);
                        SendMessageToGoogleSpaceAsync($"Detected: {message_type} [size: {message_size}px; detection quality: {message_quality}; streamId: {message_streamId} ] at {now.ToLocalTime()} {imageString}", webhookUrl);
                    }
                    else
                    {
                        Console.WriteLine($"Error: {message_type} [size: {message_size}px; detection quality: {message_quality}; streamId: {message_streamId} ] at {now.ToLocalTime()} | {imageString}", webhookUrl);
                    }

                    
                }, onError: err => Console.WriteLine("Error:"+err));

            while (true)
            {
                
            }
        }

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
