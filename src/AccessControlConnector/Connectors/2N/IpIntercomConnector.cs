using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.NN
{
    public class IpIntercomConnector : IAccessControlConnector
    {
        private readonly ILogger logger;
        private readonly IHttpClientFactory httpClientFactory;

        public IpIntercomConnector(
            ILogger logger,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            this.logger.Information("OpenAsync to {host}:{port} for {reader} and channel {channel}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Reader, accessControlMapping.Channel);

            if (!string.IsNullOrEmpty(accessControlUserId) && accessControlMapping.Channel != null)
            {
                await SendOpenToAccessPointAsync(
                    accessControlMapping.Schema,
                    accessControlMapping.Host,
                    accessControlMapping.Port ?? 80,
                    accessControlMapping.Username,
                    accessControlMapping.Password,
                    accessControlMapping.Channel,
                    accessControlUserId
                );

                return;
            }

            if (accessControlMapping.Switch != null && accessControlMapping.Action != null && accessControlMapping.Reader == null)
            {
                await SendOpenToSwitchAsync(
                    accessControlMapping.Schema,
                    accessControlMapping.Host,
                    accessControlMapping.Port ?? 80,
                    accessControlMapping.Username,
                    accessControlMapping.Password,
                    accessControlMapping.Switch,
                    accessControlMapping.Action,
                    accessControlMapping.Params
                );

                return;
            }

            await SendOpenToControlAsync(
                accessControlMapping.Schema,
                accessControlMapping.Host,
                accessControlMapping.Port ?? 80,
                accessControlMapping.Username,
                accessControlMapping.Password,
                accessControlMapping.Reader,
                accessControlMapping.Action
            );

            return;
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        public Task DenyAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            return Task.CompletedTask;
        }

        public Task BlockAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            return Task.CompletedTask;
        }

        private async Task SendOpenToAccessPointAsync(string scheme, string host, int? port, string username, string password, int? channel, string userId)
        {
            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"{scheme ?? "http"}://{host}:{port}/api/accesspoint/grantaccess?id={channel}&user={userId}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            this.logger.Information($"{nameof(SendOpenToAccessPointAsync)} to {requestUri}");

            var httpResponse = await httpClient.SendAsync(httpRequest);

            httpResponse.EnsureSuccessStatusCode();

            this.logger.Information("Status {httpStatus}", (int)httpResponse.StatusCode);
        }

        private async Task SendOpenToSwitchAsync(string scheme, string host, int? port, string username, string password, string @switch, string action, string @params)
        {
            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"{scheme ?? "http"}://{host}:{port}/api/switch/ctrl?switch={@switch}&action={action}&response={@params}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            this.logger.Information($"{nameof(SendOpenToSwitchAsync)} to {requestUri}");

            var httpResponse = await httpClient.SendAsync(httpRequest);

            httpResponse.EnsureSuccessStatusCode();

            this.logger.Information("Status {httpStatus}", (int)httpResponse.StatusCode);
        }

        private async Task SendOpenToControlAsync(string scheme, string host, int? port, string username, string password, string reader, string action)
        {
            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"{scheme ?? "http"}://{host}:{port}/api/io/ctrl?port={reader}&action={action}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            this.logger.Information($"{nameof(SendOpenToControlAsync)} to {requestUri}");

            var httpResponse = await httpClient.SendAsync(httpRequest);

            httpResponse.EnsureSuccessStatusCode();

            this.logger.Information("Status {httpStatus}", (int)httpResponse.StatusCode);
        }
    }
}