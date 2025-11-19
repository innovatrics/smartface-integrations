using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors
{
    public class AdvantechWISE4000Connector : IAccessControlConnector
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public AdvantechWISE4000Connector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(AccessConnectorConfig accessControlMapping, string accessControlUserId = null)
        {
            _logger.Information("Send Open to {host}:{port}/do_value/slot_0/ and channel: {channel}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Channel);

            var httpClient = _httpClientFactory.CreateClient();

            var port = accessControlMapping.Port ?? 80;

            var requestUri = $"{accessControlMapping.Schema ?? "http"}://{accessControlMapping.Host}:{port}/do_value/slot_0/";

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);

            if (!string.IsNullOrEmpty(accessControlMapping.Username) && !string.IsNullOrEmpty(accessControlMapping.Password))
            {
                var authenticationString = $"{accessControlMapping.Username}:{accessControlMapping.Password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var payload = new
            {
                DOVal = new[] {
                    new {
                        Ch = accessControlMapping.Channel,
                        Val = 1
                    },
                    new {
                        Ch = accessControlMapping.Channel,
                        Val = 0
                    }
                }
            };

            httpRequestMessage.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            var httpRequest = await httpClient.SendAsync(httpRequestMessage);
            string resultContent = await httpRequest.Content.ReadAsStringAsync();

            if (httpRequest.IsSuccessStatusCode)
            {
                _logger.Information("OK");
            }
            else
            {
                _logger.Error("Fail with {statusCode}", httpRequest.StatusCode);
            }
        }

        public async Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null,string username = null, string password = null)
        {
            _logger.Information("Send KeepAlive to {host}:{port}/di_value/slot_0/ and channel: {channel}", host, port, channel);

            var httpClient = _httpClientFactory.CreateClient();

            var requestUri = $"{schema ?? "http"}://{host}:{port}/di_value/slot_0/";

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
                _logger.Information("OK");
            }
            else
            {
                _logger.Error("Fail with {statusCode}", result.StatusCode);
            }
        }
    }
}