using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Telemetry;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors
{
    public class TrafficLightConnector : IAccessControlConnector
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

        public async Task OpenAsync(StreamConfig streamConfig, string accessControlUserId = null)
        {
            this.logger.Information("Send Open to {host}:{port}/go", streamConfig.Host, streamConfig.Port);

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"{streamConfig.Schema ?? "http"}://{streamConfig.Host}:{streamConfig.Port}/go";

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);

            using var activity = AccessControlTelemetry.ActivitySource.StartActivity(
                AccessControlTelemetry.ExternalCallSpanName,
                ActivityKind.Client);

            activity?.SetTag("http.method", "POST");
            activity?.SetTag("http.url", requestUri);
            activity?.SetTag(AccessControlTelemetry.ConnectorNameAttribute, "TrafficLight");

            try
            {
                var result = await httpClient.SendAsync(httpRequest);

                activity?.SetTag("http.status_code", (int)result.StatusCode);

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
            catch (HttpRequestException ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag(AccessControlTelemetry.ErrorTypeAttribute, "HttpRequestException");
                throw;
            }
        }

        public async Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null,string username = null, string password = null)
        {
            this.logger.Information("Send KeepAlive to {host}:{port}/", host, port);

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"{schema ?? "http"}://{host}:{port}/";

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