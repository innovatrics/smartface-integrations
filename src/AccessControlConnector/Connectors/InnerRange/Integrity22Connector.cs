using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models.InnerRange;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange
{
    public class Integrity22Connector : IAccessControlConnector
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public Integrity22Connector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            this.logger.Information("Send Open to {host}:{port}/do_value/slot_0/ and channel: {channel}", host, port, channel);

            var cardData = await this.getCardDataAsync(host, port, username, password, accessControlUserId);

            await this.sendOpenAsync(host, port, username, password, accessControlUserId);

            return;












            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"http://{host}:{port}/do_value/slot_0/";

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
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

            if (result.IsSuccessStatusCode)
            {
                this.logger.Information("OK");
            }
            else
            {
                this.logger.Error("Fail with {statusCode}", result.StatusCode);
            }
        }

        public async Task SendKeepAliveAsync(string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            this.logger.Information("Send KeepAlive to {host}:{port}/di_value/slot_0/ and channel: {channel}", host, port, channel);

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"http://{host}:{port}/di_value/slot_0/";

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

        private async Task<string> getCardDataAsync(string host, int? port, string username, string password, string cardNumber)
        {
            var cardDirectory = Path.Combine(AppContext.BaseDirectory, "data", "cards");

            if (!Directory.Exists(cardDirectory))
            {
                Directory.CreateDirectory(cardDirectory);
            }

            var filePath = Path.Combine(cardDirectory, cardNumber);

            if (File.Exists(filePath))
            {
                return await File.ReadAllTextAsync(filePath);
            }

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"http://{host}:{port}/DB/Card?Notes={cardNumber}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var result = await httpClient.SendAsync(httpRequest);
            string resultContent = await result.Content.ReadAsStringAsync();

            var cardResults = XmlHelper.DeserializeXml<CardResults>(resultContent);

            if (cardResults?.Count != 1)
            {
                return null;
            }

            var card = cardResults.Cards.Single();

            await File.WriteAllTextAsync(filePath, card.CardData);

            return card.CardData;
        }

        private async Task<string> sendOpenAsync(string host, int? port, string username, string password, string cardNumber)
        {
            var cardDirectory = Path.Combine(AppContext.BaseDirectory, "data", "cards");

            if (!Directory.Exists(cardDirectory))
            {
                Directory.CreateDirectory(cardDirectory);
            }

            var filePath = Path.Combine(cardDirectory, cardNumber);

            if (File.Exists(filePath))
            {
                return await File.ReadAllTextAsync(filePath);
            }

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"http://{host}:{port}/DB/Card?Notes={cardNumber}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var result = await httpClient.SendAsync(httpRequest);
            string resultContent = await result.Content.ReadAsStringAsync();

            var cardResults = XmlHelper.DeserializeXml<CardResults>(resultContent);

            if (cardResults?.Count != 1)
            {
                return null;
            }

            var card = cardResults.Cards.Single();

            await File.WriteAllTextAsync(filePath, card.CardData);

            return card.CardData;
        }
    }
}