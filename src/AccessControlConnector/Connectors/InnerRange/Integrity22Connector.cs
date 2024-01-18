using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
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

        public async Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            this.logger.Information("Send Open to {host}:{port}/do_value/slot_0/ and channel: {channel}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Channel);

            var cardData = await this.getCardDataAsync(
                accessControlMapping.Host, 
                accessControlMapping.Port, 
                accessControlMapping.Username, 
                accessControlMapping.Password, 
                accessControlUserId
            );

            await this.sendOpenAsync(
                accessControlMapping.Host, 
                accessControlMapping.Port, 
                accessControlMapping.Username, 
                accessControlMapping.Password, 
                cardData,
                accessControlMapping.Reader,
                accessControlMapping.Channel.Value
            );

            return;
        }

        public Task SendKeepAliveAsync(string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
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

            var requestUri = $"http://{host}:{port}/DB/Card?CardNumber={cardNumber}";

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

        private async Task sendOpenAsync(string host, int? port, string username, string password, string cardData, string readerModuleID, int readerNumber)
        {
            var cardDirectory = Path.Combine(AppContext.BaseDirectory, "data", "cards");

            var httpClient = this.httpClientFactory.CreateClient();

            // http://192.168.10.22:15108/CardBadge?CardData=250000000000000047D4A3D1&ReaderModuleID=77407156193722391&ReaderNumber=2 
            var requestUri = $"http://{host}:{port}/CardBadge?CardData={cardData}&ReaderModuleID={readerModuleID}&ReaderNumber={readerNumber}";

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var httpRequest = await httpClient.SendAsync(httpRequestMessage);
            
            httpRequest.EnsureSuccessStatusCode();
        }
    }
}