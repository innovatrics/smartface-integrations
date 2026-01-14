using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Telemetry;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.AXIS
{
    public class IOPortConnector : IAccessControlConnector
    {
        private readonly ILogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public IOPortConnector(
            ILogger logger,
            IHttpClientFactory httpClientFactory
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(StreamConfig streamConfig, string accessControlUserId = null)
        {
            _logger.Information("OpenAsync to {host}:{port} for {reader} and channel {channel}", streamConfig.Host, streamConfig.Port, streamConfig.Reader, streamConfig.Channel);

            await SendOpenAsync(
                streamConfig.Schema,
                streamConfig.Host,
                streamConfig.Port ?? 80,
                streamConfig.Username,
                streamConfig.Password,
                streamConfig.Params
            );

            return;
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        private async Task SendOpenAsync(string scheme, string host, int? port, string username, string password, string @params)
        {
            var httpClient = _httpClientFactory.CreateClient();

            var requestUri = $"{scheme ?? "http"}://{host}:{port}/axis-cgi/io/port.cgi?{@params}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            _logger.Information("SendOpenAsync to {url} with {body}", requestUri);

            using var activity = AccessControlTelemetry.ActivitySource.StartActivity(
                AccessControlTelemetry.ExternalCallSpanName,
                ActivityKind.Client);

            activity?.SetTag("http.method", "GET");
            activity?.SetTag("http.url", $"{scheme ?? "http"}://{host}:{port}/axis-cgi/io/port.cgi");
            activity?.SetTag(AccessControlTelemetry.ConnectorNameAttribute, "AXIS.IOPort");

            try
            {
                var httpResponse = await httpClient.SendAsync(httpRequest);

                activity?.SetTag("http.status_code", (int)httpResponse.StatusCode);

                httpResponse.EnsureSuccessStatusCode();

                _logger.Information("Status {httpStatus}", (int)httpResponse.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag(AccessControlTelemetry.ErrorTypeAttribute, "HttpRequestException");
                throw;
            }
        }
    }
}