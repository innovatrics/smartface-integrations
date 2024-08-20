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

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.AXIS
{
    public class A1001Connector : IAccessControlConnector
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public A1001Connector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            this.logger.Information("OpenAsync to {host}:{port} for {reader} and channel {channel}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Reader, accessControlMapping.Channel);

            await this.sendOpenAsync(
                accessControlMapping.Schema,
                accessControlMapping.Host,
                accessControlMapping.Port ?? 80,
                accessControlMapping.Username,
                accessControlMapping.Password,
                accessControlMapping.Token
            );

            return;
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        private async Task sendOpenAsync(string scheme, string host, int? port, string username, string password, string token)
        {
            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"{scheme ?? "http"}://{host}:{port}/vapix/doorcontrol";

            this.logger.Information("sendOpenAsync to {url}", requestUri);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);

            var jsonContent = @"{
                                ""tdc:AccessDoor"": {
                                    ""Token"": """ + token + @"""
                                }
                            }";

            // Add content to the request
            httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var httpResponse = await httpClient.SendAsync(httpRequest);

            httpResponse.EnsureSuccessStatusCode();

            this.logger.Information("Status {httpStatus}", (int)httpResponse.StatusCode);
        }
    }
}