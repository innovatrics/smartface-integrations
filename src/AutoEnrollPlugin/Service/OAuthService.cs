using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Newtonsoft.Json;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class OAuthService
    {
        private readonly ILogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly string _tokenUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _audience;

        private OAuthToken _lastToken;

        public bool IsEnabled => _tokenUrl != null && _clientId != null && _clientSecret != null;

        public OAuthService(ILogger logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            _tokenUrl = configuration.GetValue<string>("Source:OAuth:Url");
            _clientId = configuration.GetValue<string>("Source:OAuth:ClientId");
            _clientSecret = configuration.GetValue<string>("Source:OAuth:ClientSecret");
            _audience = configuration.GetValue<string>("Source:OAuth:Audience");
        }

        public async Task<string> GetTokenAsync()
        {
            if (_lastToken == null || _lastToken.ExpiresAt <= DateTime.UtcNow)
            {
                _lastToken = await GetTokenInternalAsync();
            }

            return _lastToken.AccessToken;
        }

        private async Task<OAuthToken> GetTokenInternalAsync()
        {
            _logger.Information("Get OAuth token from endpoint {url} for client_id {clientId}", _tokenUrl, _clientId);

            var requestBody = new Dictionary<string, string>
            {
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "grant_type", "client_credentials" },
                { "audience", _audience }
            };

            var httpClient = _httpClientFactory.CreateClient();

            var requestContent = new FormUrlEncodedContent(requestBody);
            var response = await httpClient.PostAsync(_tokenUrl, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Unable to retrieve JWT token.");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<OAuthResponse>(jsonResponse);

            return new OAuthToken
            {
                AccessToken = tokenResponse.AccessToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).AddSeconds(-5)
            };
        }
    }

    public class OAuthResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }
    }

    public class OAuthToken
    {
        public string AccessToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}