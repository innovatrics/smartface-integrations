using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;
using Innovatrics.SmartFace.Integrations.MyQConnectorNamespace;
using Newtonsoft.Json;
using Innovatrics.SmartFace.Integrations.AeosSync.Nswag;
using Innovatrics.SmartFace.Models.API;
using Newtonsoft.Json.Linq;
using System.Linq;


namespace Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Connectors
{
    public class MyQConnector : IMyQConnector
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private Socket socket;
        private string PrinterConnection;

        private string clientId;
        private string clientSecret;
        private string scope;
        private int loginInfoType;
        private string MyQHostname;
        private int MyQPort;
        private string SmartFaceURL;

        public MyQConnector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.socket = null;
            
            clientId = configuration.GetValue<string>("MyQConfiguration:clientId") ?? throw new InvalidOperationException("clientId is required");
            clientSecret = configuration.GetValue<string>("MyQConfiguration:clientSecret") ?? throw new InvalidOperationException("clientSecret is required");
            scope = configuration.GetValue<string>("MyQConfiguration:scope") ?? throw new InvalidOperationException("scope is required");
            loginInfoType = configuration.GetValue<int>("MyQConfiguration:loginInfoType");
            MyQHostname = configuration.GetValue<string>("MyQConfiguration:MyQHostname") ?? throw new InvalidOperationException("MyQHostname is required");
            MyQPort = configuration.GetValue<int>("MyQConfiguration:MyQPort");
            SmartFaceURL = configuration.GetValue<string>("MyQConfiguration:SmartFaceURL");
        }

        private async Task<Socket> CreateOpenSocketAsync(string myqHostname, int myqPort)
        {
            try
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(myqHostname, myqPort);

                this.logger.Information($"Socket connected to {myqHostname}:{myqPort}");

                return socket;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"Socket failed to connect to {myqHostname}:{myqPort}");
                throw ex;
            }
        }

        public async Task OpenAsync(string myqPrinter, Guid myqStreamId, string watchlistMemberId)
        {
            this.logger.Information("MyQ Printer Unlocking");

            /*  
            this.logger.Information(this.clientId);
            this.logger.Information(this.clientSecret);
            this.logger.Information(this.scope);
            this.logger.Information((this.loginInfoType).ToString());
            this.logger.Information(this.MyQHostname);
            this.logger.Information((this.MyQPort).ToString());
            */

            // DO REST API CALLS

                /*

                - check if the printer is already unlocked
                - if it is not unlocked proceed
                - find out email address of the user from the SmartFace, use the email as a card token
                - get authentication token DONE
                - unlock printer DONE

                */

                this.logger.Information($"WatchlistMemberID: {watchlistMemberId}");              

                // Define OAuth2 parameters
                string tokenEndpoint = $"https://{MyQHostname}:{MyQPort}/api/auth/token";
                string loginInfoCard;

                // Create an instance of HttpClientHandler with SSL certificate validation bypassed
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                // Create an instance of HttpClient with the custom handler
                using (HttpClient client = new HttpClient(handler))
                {
                    try
                    {
                        var smartfaceGetWLMEndpointUrl = $"{SmartFaceURL}/api/v1/WatchlistMembers/{watchlistMemberId}";

                        // get user email address from the SmartFace here:
                         // Make a POST request with JSON payload to the subsequent API endpoint
                        HttpResponseMessage getEmailResponse = await client.GetAsync(smartfaceGetWLMEndpointUrl);

                        // Check if the response is successful (status code 200)
                        if (getEmailResponse.IsSuccessStatusCode)
                        {
                            // Read the response content as a string
                            string responseData = await getEmailResponse.Content.ReadAsStringAsync();
                            this.logger.Information($"Returned Data: {responseData}" );

                            // Parse the JSON response
                            var jsonObject = JObject.Parse(responseData);

                            // Access the value associated with the key "value" under the "email" label
                            string email = jsonObject["labels"].FirstOrDefault(l => l["key"].ToString() == "email")?["value"]?.ToString();
                            // Get the value associated with the key "displayName"
                            string displayName = jsonObject["displayName"]?.ToString();

                            // Output the email value
                            this.logger.Information($"Email of {displayName}: {email}");

                            loginInfoCard = email;

                            // Create JSON payload for the token request
                            var jsonInput = JsonConvert.SerializeObject(new {
                                grant_type = "login_info",
                                client_id = clientId,
                                client_secret = clientSecret,
                                login_info = new {
                                    type = loginInfoType,
                                    card = loginInfoCard
                                },
                                scope
                            });

                            // Convert JSON payload to StringContent
                            var tokenpayloadContent = new StringContent(jsonInput, Encoding.UTF8, "application/json");

                            // Make a POST request to the token endpoint
                            HttpResponseMessage tokenResponse = await client.PostAsync(tokenEndpoint, tokenpayloadContent);
                            tokenResponse.EnsureSuccessStatusCode();

                            string responseBody = await tokenResponse.Content.ReadAsStringAsync();

                            var tokenJson = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
                            string accessToken = tokenJson.Value<string>("access_token");

                            // Use the obtained access token for subsequent requests
                            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                            // Specify the URL of the REST API endpoint
                            string apiUrl = $"https://{MyQHostname}:{MyQPort}/api/v3/printers/unlock";

                            // Create JSON payload for the subsequent API call
                            string jsonPayload = @"
                            {
                                ""sn"": """ + myqPrinter + @""",
                                ""account"": """ + accessToken + @"""
                            }";

                            var jsonRequestInput = JsonConvert.SerializeObject(new{
                                sn = myqPrinter,
                                account = accessToken
                            });
                            
                            var payloadContent = new StringContent(jsonRequestInput, Encoding.UTF8, "application/json");

                            // Make a POST request with JSON payload to the subsequent API endpoint
                            HttpResponseMessage response = await client.PostAsync(apiUrl, payloadContent);

                            // Check if the response is successful (status code 200)
                            if (response.IsSuccessStatusCode)
                            {
                                // Read the response content as a string
                                string responseDataUnlock = await response.Content.ReadAsStringAsync();
                                this.logger.Information($"Printer {myqPrinter} unlocked on streamId {myqStreamId}");
                            }
                            else
                            {
                                // If the response is not successful, display the status code
                                this.logger.Warning($"Unlocking failed for printer {myqPrinter}. " + response.StatusCode + "; " + await response.Content.ReadAsStringAsync());
                            }
                        }
                        else
                        {
                            // If the response is not successful, display the status code
                            this.logger.Error($"Error while returning WLM data: " + getEmailResponse.StatusCode + "; " + await getEmailResponse.Content.ReadAsStringAsync());
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        // Handle any exceptions that occur during the API call
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }

        }

    }
}