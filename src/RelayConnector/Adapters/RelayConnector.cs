using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.RelayConnector
{
    public class RelayConnector : IRelayConnector
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public RelayConnector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(string ipAddress, int port, int channel, string authUsername = null, string authPassword = null)
        {
            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"http://{ipAddress}:{port}/do_value/slot_0/";

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);

            if (!string.IsNullOrEmpty(authUsername) && !string.IsNullOrEmpty(authPassword))
            {
                var authenticationString = $"{authUsername}:{authPassword}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var payload = new
            {
                DOVal = new[] {
                    new {
                        Ch = channel,
                        Val = 1
                    },
                    new {
                        Ch = channel,
                        Val = 0
                    }
                }
            };

            httpRequest.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            var result = await httpClient.SendAsync(httpRequest);
            string resultContent = await result.Content.ReadAsStringAsync();

            this.logger.Debug("Response: {response}", resultContent);
        }
    }
}