using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Serilog;

using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;
using Newtonsoft.Json.Linq;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class OAuthService : IOAuthService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private readonly string tokenUrl;
        private readonly string clientId;
        private readonly string clientSecret;
        private readonly string scope;

        public bool IsEnabled => this.tokenUrl != null && this.clientId != null && this.scope != null;
        
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
            this.scope = this.configuration.GetValue<string>("Source:OAuth:Scope");
        }

        public async Task<string> GetTokenAsync()
        {
            this.logger.Information("Get OAuth token from endpoint {url} for client_id {clientId} and scope {scope}", this.tokenUrl, this.clientId, this.scope);

            var requestBody = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "client_credentials" },
                { "scope", scope }
            };

            var httpClient = this.httpClientFactory.CreateClient();

            var requestContent = new FormUrlEncodedContent(requestBody);
            var response = await httpClient.PostAsync(tokenUrl, requestContent);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenObj = JObject.Parse(jsonResponse);
                return tokenObj["access_token"]?.ToString();
            }

            throw new Exception("Unable to retrieve JWT token.");
        }
    }
}