using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors
{
    public class SharryCheckInConnector : IAccessControlConnector
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly string _sharryApiUrl;

        public SharryCheckInConnector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            _sharryApiUrl = _configuration.GetValue<string>("SharryConfiguration:ApiUrl");
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        public async Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            try
            {
                _logger.Information("Processing Sharry check-in for stream {StreamId}", accessControlMapping?.StreamId);

                var doorId = accessControlMapping?.StreamId.ToString();

                // Parse the data from the UserResolver (passed as JSON string with MemberId + labels)
                System.Collections.Generic.Dictionary<string, string> userData = null;
                if (!string.IsNullOrEmpty(accessControlUserId))
                {
                    try
                    {
                        userData = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(accessControlUserId);
                    }
                    catch (JsonException ex)
                    {
                        _logger.Warning(ex, "Failed to parse user data, using raw value as memberId");
                        userData = new System.Collections.Generic.Dictionary<string, string> { { "MemberId", accessControlUserId } };
                    }
                }

                var sharryId = GetValue(userData, "Sharry_Id");
                var memberId = GetValue(userData, "MemberId");
                var qrToken = GetValue(userData, "Integrity_QR_Token");
                var faceToken = GetValue(userData, "Integrity_Face_Token");

                // Validate required fields
                if (string.IsNullOrEmpty(sharryId))
                {
                    _logger.Information("Sharry check-in skipped: SharryId is missing for stream {StreamId}", accessControlMapping?.StreamId);
                    return;
                }

                if (string.IsNullOrEmpty(qrToken) && string.IsNullOrEmpty(faceToken))
                {
                    _logger.Information("Sharry check-in skipped: Both QrToken and FaceToken are missing for stream {StreamId}", accessControlMapping?.StreamId);
                    return;
                }

                var payload = new
                {
                    sharryId = sharryId,
                    memberId = memberId,
                    QrToken = qrToken,
                    FaceToken = faceToken,
                    doorId = doorId
                };

                _logger.Information("Sharry check-in payload: {@Payload}", payload);

                // TODO: Uncomment when valid Sharry API URL and authentication are available
                // await SendCheckInRequestAsync(payload);

                _logger.Information("Sharry check-in completed for stream {StreamId}", accessControlMapping?.StreamId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to process Sharry check-in for stream {StreamId}", accessControlMapping?.StreamId);
                throw;
            }
        }

        private async Task SendCheckInRequestAsync(object payload)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // TODO: Add authentication headers when auth mechanism is defined

            var response = await httpClient.PostAsync(_sharryApiUrl, content);
            response.EnsureSuccessStatusCode();

            _logger.Information("Sharry API responded with status {StatusCode}", response.StatusCode);
        }

        private static string GetValue(System.Collections.Generic.Dictionary<string, string> dict, string key)
        {
            if (dict == null)
            {
                return null;
            }

            return dict.TryGetValue(key, out var value) ? value : null;
        }
    }
}

