using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models.InnerRange;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Telemetry;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange
{
    public class Integriti22Connector : IAccessControlConnector
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private IntegritiConfiguration _integritiConfiguration;
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

        public async Task OpenAsync(StreamConfig streamConfig, string accessControlUserId = null)
        {
            if (_integritiConfiguration == null)
            {
                _integritiConfiguration = _configuration.GetSection("Integriti22").Get<IntegritiConfiguration>();
            }

            var schema = streamConfig.Schema ?? _integritiConfiguration.Schema;
            var host = streamConfig.Host ?? _integritiConfiguration.Host;
            var port = streamConfig.Port ?? _integritiConfiguration.Port;
            var username = streamConfig.Username ?? _integritiConfiguration.Username;
            var password = streamConfig.Password ?? _integritiConfiguration.Password;
            var controller = streamConfig.Controller ?? _integritiConfiguration.Controller;

            _logger.Information($"{nameof(OpenAsync)} to {{host}}:{{port}} for {{doorName}}, {{doorId}}, {{controller}}, {{reader}} and {{channel}}", host, port, streamConfig.DoorName, streamConfig.DoorId, controller, streamConfig.Reader, streamConfig.Channel);

            string cardData = ApplyCardMask(accessControlUserId, _integritiConfiguration.CardMask);

            if (cardData == null)
            {
                _logger.Warning("No CardData found for {accessControlUserId}", accessControlUserId);
                return;
            }

            await SendOpenAsync(
                schema,
                host,
                port,
                username,
                password,
                cardData,
                streamConfig.Reader,
                streamConfig.Channel,
                streamConfig.DoorName,
                streamConfig.DoorId,
                controller
            );

            return;
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        private string ApplyCardMask(string accessControlUserId, string cardMask)
        {
            if (string.IsNullOrEmpty(accessControlUserId))
            {
                return null;
            }

            if (string.IsNullOrEmpty(cardMask))
            {
                return accessControlUserId;
            }

            if (cardMask.Contains("{0}"))
            {
                try
                {
                    return string.Format(cardMask, accessControlUserId);
                }
                catch (FormatException ex)
                {
                    _logger.Warning("Invalid card mask format '{cardMask}': {error}. Returning accessControlUserId as-is.", cardMask, ex.Message);
                    return accessControlUserId;
                }
            }

            if (accessControlUserId.StartsWith(cardMask))
            {
                return accessControlUserId;
            }

            return accessControlUserId;
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

            string requestUri;

            if (!string.IsNullOrEmpty(doorName) && !string.IsNullOrEmpty(controller))
            {
                if (!string.IsNullOrEmpty(doorId))
                {
                    requestUri = $"{schema ?? "http"}://{host}:{port}/CardBadge" +
                                 $"?CardData={cardData}&CardBitLength=32&DoorId={doorId}&Controller={controller}";
                }
                else
                {
                    requestUri = $"{schema ?? "http"}://{host}:{port}/CardBadge" +
                                 $"?CardData={cardData}&CardBitLength=32&DoorName={doorName}&Controller={controller}";
                }
            }
            else
            {
                requestUri = $"{schema ?? "http"}://{host}:{port}/CardBadge" +
                             $"?CardData={cardData}&CardBitLength=32&ReaderModuleID={readerModuleID}&ReaderNumber={readerNumber}";
            }

            _logger.Information("Url: {url}", requestUri);

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            using var activity = AccessControlTelemetry.ActivitySource.StartActivity(
                AccessControlTelemetry.ExternalCallSpanName,
                ActivityKind.Client);

            activity?.SetTag("http.method", "GET");
            activity?.SetTag("http.url", $"{schema ?? "http"}://{host}:{port}/CardBadge");
            activity?.SetTag(AccessControlTelemetry.ConnectorNameAttribute, "InnerRange.Integriti22");

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