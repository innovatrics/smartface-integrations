using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Serilog;
using Newtonsoft.Json.Linq;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class OAuthService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private readonly string tokenUrl;
        private readonly string clientId;
        private readonly string clientSecret;
        private readonly string audience;

        public bool IsEnabled => this.tokenUrl != null && this.clientId != null && this.clientSecret != null;

        private OAuthToken lastToken;

        public OAuthService(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            this.tokenUrl = this.configuration.GetValue<string>("Source:OAuth:Url");
            this.clientId = this.configuration.GetValue<string>("Source:OAuth:ClientId");
            this.clientSecret = this.configuration.GetValue<string>("Source:OAuth:ClientSecret");
            this.audience = this.configuration.GetValue<string>("Source:OAuth:Audience");
        }

        public async Task<string> GetTokenAsync()
        {
            if (this.lastToken == null || this.lastToken.ExpiresAt <= DateTime.UtcNow)
            {
                this.lastToken = await this.getTokenAsync();
            }

            return this.lastToken.AccessToken;
        }

        private async Task<OAuthToken> getTokenAsync()
        {
            this.logger.Information("Get OAuth token from endpoint {url} for client_id {clientId}", this.tokenUrl, this.clientId);

            var requestBody = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "client_credentials" },
                { "audience", audience }
            };

            var httpClient = this.httpClientFactory.CreateClient();

            var requestContent = new FormUrlEncodedContent(requestBody);
            var response = await httpClient.PostAsync(tokenUrl, requestContent);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<OAuthResponse>(jsonResponse);

                return new OAuthToken()
                {
                    AccessToken = tokenResponse.AccessToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).AddSeconds(-5)
                };
            }

            throw new Exception("Unable to retrieve JWT token.");
        }
    }
}