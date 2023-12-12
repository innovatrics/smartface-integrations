using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.RelayConnector.Connectors
{
    public class TrafficLightConnector : IRelayConnector
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public TrafficLightConnector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            this.logger.Information("Traffic Light Connector Created!");
        }

        public async Task OpenAsync(string ipAddress, int port, int channel, string username = null, string password = null)
        {
            this.logger.Information("Send Open to {ipAddress}:{port}/go", ipAddress, port);

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"http://{ipAddress}:{port}/go";

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);

            var result = await httpClient.SendAsync(httpRequest);
            string resultContent = await result.Content.ReadAsStringAsync();

            if (result.IsSuccessStatusCode)
            {
                this.logger.Information("OK");
            }
            else
            {
                this.logger.Error("Fail with {statusCode}", result.StatusCode);
            }
        }

        public async Task SendKeepAliveAsync(string ipAddress, int port, int? channel = null, string username = null, string password = null)
        {
            this.logger.Information("Send KeepAlive to {ipAddress}:{port}/", ipAddress, port);

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"http://{ipAddress}:{port}/";

            if (channel != null)
            {
                requestUri += $"{channel}";
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var result = await httpClient.SendAsync(httpRequest);
            string resultContent = await result.Content.ReadAsStringAsync();

            if (result.IsSuccessStatusCode)
            {
                this.logger.Information("OK");
            }
            else
            {
                this.logger.Error("Fail with {statusCode}", result.StatusCode);
            }
        }
    }
}