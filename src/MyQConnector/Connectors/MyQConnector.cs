using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.MyQConnector.Connectors
{
    public class Connector : IConnector
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private string clientId;
        private string clientSecret;
        private string scope;
        private int loginInfoType;
        private string myQHostname;
        private int myQPort;
        private string smartFaceURL;
        private bool bypassSslValidation;

        public Connector(ILogger logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            clientId = configuration.GetValue<string>("MyQConfiguration:clientId") ?? throw new InvalidOperationException("clientId is required");
            clientSecret = configuration.GetValue<string>("MyQConfiguration:clientSecret") ?? throw new InvalidOperationException("clientSecret is required");
            scope = configuration.GetValue<string>("MyQConfiguration:scope") ?? throw new InvalidOperationException("scope is required");
            loginInfoType = configuration.GetValue<int>("MyQConfiguration:loginInfoType");
            myQHostname = configuration.GetValue<string>("MyQConfiguration:MyQHostname") ?? throw new InvalidOperationException("MyQHostname is required");
            myQPort = configuration.GetValue<int>("MyQConfiguration:MyQPort");
            smartFaceURL = configuration.GetValue<string>("MyQConfiguration:SmartFaceURL");
            bypassSslValidation = configuration.GetValue<bool>("MyQConfiguration:BypassSslValidation");
        }

        public async Task OpenAsync(string myqPrinter, Guid myqStreamId, string watchlistMemberId)
        {
            this.logger.Information("MyQ Printer Unlocking");
            this.logger.Information($"WatchlistMemberID: {watchlistMemberId}");

            try
            {
                string email = await GetEmailFromSmartFaceAPI(watchlistMemberId);
                string token = await AuthenticateWithMyQAPI();
                string userInfo = await GetUserInfo(email, token);
                string userToken = await AuthenticateUserWithMyQAPI(userInfo);
                await UnlockPrinter(myqPrinter, myqStreamId, userToken);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Error occurred in OpenAsync");
                throw;
            }
        }

        private HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();
            if (bypassSslValidation)
            {
                // Bypass SSL certificate validation in non-production environments
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            }
            return new HttpClient(handler);
        }

        private async Task<string> GetEmailFromSmartFaceAPI(string watchlistMemberId)
        {
            var client = CreateHttpClient();
            var url = $"{smartFaceURL}/api/v1/WatchlistMembers/{watchlistMemberId}";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var jsonObject = JObject.Parse(content);
            string email = jsonObject["labels"].FirstOrDefault(l => l["key"].ToString() == "email")?["value"]?.ToString();

            this.logger.Information($"Email retrieved: {email}");
            return email;
        }

        private async Task<string> AuthenticateWithMyQAPI()
        {
            var client = CreateHttpClient();
            string tokenEndpoint = $"https://{myQHostname}:{myQPort}/api/auth/token";
            var payload = new
            {
                grant_type = "client_credentials",
                client_id = clientId,
                client_secret = clientSecret,
                scope
            };

            var response = await client.PostAsync(tokenEndpoint, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            var tokenJson = JObject.Parse(await response.Content.ReadAsStringAsync());
            string accessToken = tokenJson.Value<string>("access_token");

            this.logger.Information("Authentication successful");
            return accessToken;
        }

        private string ExtractUsernameFromJson(string json)
        {
            var jsonObject = JObject.Parse(json);
            var usersArray = (JArray)jsonObject["users"];
            if (usersArray != null && usersArray.Count > 0)
            {
                var firstUser = (JObject)usersArray[0];
                return firstUser["username"]?.ToString();  // Safely accessing the username
            }
            return null;  // Return null if no users are found
        }


        private async Task<string> AuthenticateUserWithMyQAPI(string userInfo)
        {
            var client = CreateHttpClient();
            string tokenEndpoint = $"https://{myQHostname}:{myQPort}/api/auth/token";
            string username = ExtractUsernameFromJson(userInfo);
            if(username == null)
            {
                return "";
            }

            var payload = new
            {
                grant_type = "login_info",
                client_id = clientId,
                client_secret = clientSecret,
                login_info = new
                {
                    type = loginInfoType,
                    card = username  // Ensure 'card' is the correct field to send, it might need to be something else.
                },
                scope
            };

            HttpResponseMessage response = null;
            try
            {
                response = await client.PostAsync(tokenEndpoint, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var tokenJson = JObject.Parse(await response.Content.ReadAsStringAsync());
                string userAccessToken = tokenJson.Value<string>("access_token");

                this.logger.Information("User-specific authentication successful");
                return userAccessToken;
            }
            catch (HttpRequestException e)
            {
                string errorContent = response != null ? await response.Content.ReadAsStringAsync() : "No response content";
                this.logger.Error($"Failed to authenticate user. Status Code: {response?.StatusCode}. Response: {errorContent}");
                throw;
            }
        }

        private async Task<string> GetUserInfo(string email, string token)
        {
            var client = CreateHttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            string url = $"https://{myQHostname}:{myQPort}/api/v3/users/find?email={email}";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            this.logger.Information($"User info retrieved: {content}");
            return content;
        }

        private async Task UnlockPrinter(string printer, Guid streamId, string userToken)
        {
            var client = CreateHttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

            string apiUrl = $"https://{myQHostname}:{myQPort}/api/v3/printers/unlock";
            var payload = new { sn = printer, account = userToken };

            var response = await client.PostAsync(apiUrl, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
            if (response.IsSuccessStatusCode)
            {
                this.logger.Information($"Printer {printer} unlocked on streamId {streamId}");
            }
            else
            {
                this.logger.Warning($"Unlocking failed for printer {printer}. Status: {response.StatusCode}, Message: {await response.Content.ReadAsStringAsync()}");
            }
        }
    }
}
