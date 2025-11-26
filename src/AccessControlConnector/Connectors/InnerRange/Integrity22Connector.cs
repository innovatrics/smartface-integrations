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
    public class Integriti22Connector : IAccessControlConnector
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public Integriti22Connector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            _logger.Information($"{nameof(OpenAsync)} to {{host}}:{{port}} for {{doorName}}, {{doorId}}, {{controller}}, {{reader}} and {{channel}}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.DoorName, accessControlMapping.DoorId, accessControlMapping.Controller, accessControlMapping.Reader, accessControlMapping.Channel);

            string cardData = null;

            if (!string.IsNullOrEmpty(accessControlUserId))
            {
                if (accessControlUserId.StartsWith("2000000000000000"))
                {
                    cardData = accessControlUserId;
                }
                else
                {
                    cardData = $"20000000000000000{accessControlUserId}";
                }
            }
            else
            {
                cardData = await GetCardDataAsync(accessControlMapping.Schema, accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Username, accessControlMapping.Password, accessControlUserId);
            }

            if (cardData == null)
            {
                _logger.Warning("No CardData found for {accessControlUserId}", accessControlUserId);
                return;
            }

            await SendOpenAsync(
                accessControlMapping.Schema,
                accessControlMapping.Host,
                accessControlMapping.Port,
                accessControlMapping.Username,
                accessControlMapping.Password,
                cardData,
                accessControlMapping.Reader,
                accessControlMapping.Channel,
                accessControlMapping.DoorName,
                accessControlMapping.DoorId,
                accessControlMapping.Controller
            );

            return;
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        private async Task<string> GetCardDataAsync(string schema, string host, int? port, string username, string password, string cardNumber)
        {
            _logger.Information($"{nameof(GetCardDataAsync)} to {{schema}}:{{host}}:{{port}} for {{cardNumber}}", schema, host, port, cardNumber);

            var cardDirectory = Path.Combine(AppContext.BaseDirectory, "data", "cards");

            if (!Directory.Exists(cardDirectory))
            {
                Directory.CreateDirectory(cardDirectory);
            }

            var filePath = Path.Combine(cardDirectory, cardNumber);

            if (File.Exists(filePath))
            {
                var cardData = await File.ReadAllTextAsync(filePath);

                _logger.Information("Return {cardData} for {cardNumber} from cache", cardData, cardNumber);

                return cardData;
            }

            var httpClient = _httpClientFactory.CreateClient();

            var requestUri = $"{schema ?? "http"}://{host}:{port}/DB/Card?CardNumber={cardNumber}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var httpResponse = await httpClient.SendAsync(httpRequest);

            httpResponse.EnsureSuccessStatusCode();

            string httpResponseStringContent = await httpResponse.Content.ReadAsStringAsync();

            var cardResults = XmlHelper.DeserializeXml<CardResults>(httpResponseStringContent);

            if (cardResults?.Count != 1)
            {
                return null;
            }

            var card = cardResults.Cards.Single();

            await File.WriteAllTextAsync(filePath, card.CardData);

            return card.CardData;
        }

        private async Task SendOpenAsync(string schema, string host, int? port, string username, string password, string cardData, string readerModuleID, int? readerNumber, string doorName, string doorId, string controller)
        {
            _logger.Information($"{nameof(SendOpenAsync)} to {{schema}}:{{host}}:{{port}} for {{doorName}}, {{doorId}}, {{controller}}, {{readerModuleID}}, {{readerNumber}}, {{cardData}}", schema, host, port, doorName, doorId, controller, readerModuleID, readerNumber, cardData);

            var httpClient = _httpClientFactory.CreateClient();

            // {schema ?? "http"}://192.168.10.22:15108/CardBadge?CardData=250000000000000047D4A3D1&ReaderModuleID=77407156193722391&ReaderNumber=2 

            string requestUri;

            switch (doorName, doorId, controller)
            {
                case var (dn, _, c) when !string.IsNullOrEmpty(dn) && !string.IsNullOrEmpty(c):
                    requestUri = $"{schema ?? "http"}://{host}:{port}/CardBadge?CardData={cardData}&CardBitLength=32&DoorName={dn}&Controller={c}";
                    break;

                case var (_, did, c) when !string.IsNullOrEmpty(did) && !string.IsNullOrEmpty(c):
                    requestUri = $"{schema ?? "http"}://{host}:{port}/CardBadge?CardData={cardData}&CardBitLength=32&DoorId={did}&Controller={c}";
                    break;

                default:
                    requestUri = $"{schema ?? "http"}://{host}:{port}/CardBadge?CardData={cardData}&CardBitLength=32&ReaderModuleID={readerModuleID}&ReaderNumber={readerNumber}";
                    break;
            }

            _logger.Information("Url: {url}", requestUri);

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var httpResponse = await httpClient.SendAsync(httpRequest);

            httpResponse.EnsureSuccessStatusCode();

            _logger.Information("Status {httpStatus}", (int)httpResponse.StatusCode);
        }
    }
}