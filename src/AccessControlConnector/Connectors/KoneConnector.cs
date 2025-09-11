using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Security.Cryptography;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors
{
    public class KoneConnector : IAccessControlConnector
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _buildingId;
        private readonly string _apiHostname;
        private readonly string _webSocketEndpoint;
        private readonly string _webSocketSubprotocol;
        private readonly string _groupId;
        private readonly int _defaultTerminal;
        private readonly int _defaultArea;
        private readonly int _defaultAction;

        // OAuth token cache
        private string _cachedAccessToken;
        private DateTimeOffset _accessTokenExpiresAt;
        private readonly SemaphoreSlim _accessTokenLock = new SemaphoreSlim(1, 1);
        private readonly TimeSpan _accessTokenRefreshSkew = TimeSpan.FromSeconds(60);

        public KoneConnector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            _clientId = _configuration.GetValue<string>("KoneConfiguration:ClientId") ?? throw new InvalidOperationException("KoneConfiguration:ClientId is required");
            _clientSecret = _configuration.GetValue<string>("KoneConfiguration:ClientSecret") ?? throw new InvalidOperationException("KoneConfiguration:ClientSecret is required");
            _buildingId = _configuration.GetValue<string>("KoneConfiguration:BuildingId") ?? throw new InvalidOperationException("KoneConfiguration:BuildingId is required");
            _apiHostname = _configuration.GetValue<string>("KoneConfiguration:ApiHostname", "dev.kone.com");
            _webSocketEndpoint = _configuration.GetValue<string>("KoneConfiguration:WebSocketEndpoint", $"wss://{_apiHostname}/stream-v2");
            _webSocketSubprotocol = _configuration.GetValue<string>("KoneConfiguration:WebSocketSubprotocol", "koneapi");
            _groupId = _configuration.GetValue<string>("KoneConfiguration:GroupId", "1");
            _defaultTerminal = _configuration.GetValue<int>("KoneConfiguration:Terminal", 1);
            _defaultArea = _configuration.GetValue<int>("KoneConfiguration:Area", 1000);
            _defaultAction = _configuration.GetValue<int>("KoneConfiguration:Action", 2001);
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        public async Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            try
            {
                _logger.Information("KoneConnector OpenAsync: triggering elevator call for stream {StreamId}", accessControlMapping?.StreamId);

                using var tokenCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var token = await FetchAccessTokenAsync(tokenCts.Token);

                using var wsCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var ws = await OpenWebSocketConnectionAsync(token, wsCts.Token);

                var resolvedArea = accessControlMapping?.Area ?? _defaultArea;
                var resolvedTerminal = accessControlMapping?.Terminal ?? _defaultTerminal;

                var action = ResolveAction(accessControlMapping?.Action);
                var payload = BuildLiftCallPayload(resolvedArea, resolvedTerminal, action);

                _logger.Information("Payload is {@Payload}", payload);

                var json = JsonSerializer.Serialize(payload);
                using var sendCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await SendWebsocketMessageAsync(ws, json, sendCts.Token);

                using var recvCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var m1 = await ReceiveTextMessageAsync(ws, recvCts.Token);
                var m2 = await ReceiveTextMessageAsync(ws, recvCts.Token);

                _logger.Information("KONE response messages: {Message1} | {Message2}", m1, m2);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "KoneConnector OpenAsync failed");
                throw;
            }
        }

        private async Task<string> FetchAccessTokenAsync(CancellationToken ct = default)
        {
            // Fast path: return cached token if valid (with refresh skew)
            var now = DateTimeOffset.UtcNow;
            var cached = _cachedAccessToken;
            if (!string.IsNullOrEmpty(cached) && now < (_accessTokenExpiresAt - _accessTokenRefreshSkew))
            {
                return cached;
            }

            await _accessTokenLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Double-check after acquiring the lock
                now = DateTimeOffset.UtcNow;
                cached = _cachedAccessToken;
                if (!string.IsNullOrEmpty(cached) && now < (_accessTokenExpiresAt - _accessTokenRefreshSkew))
                {
                    return cached;
                }

                var tokenEndpointV2 = $"https://{_apiHostname}/api/v2/oauth2/token";
                var scope = $"application/inventory callgiving/group:{_buildingId}:{_groupId}";

                var httpClient = _httpClientFactory.CreateClient();

                using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpointV2);
                request.Content = new StringContent($"grant_type=client_credentials&scope={Uri.EscapeDataString(scope)}", Encoding.UTF8, "application/x-www-form-urlencoded");
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

                _logger.Information("Requesting KONE access token at {endpoint}", tokenEndpointV2);

                using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    throw new HttpRequestException($"Token request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("access_token", out var tokenEl))
                {
                    throw new InvalidOperationException("access_token missing in response");
                }
                var token = tokenEl.GetString() ?? throw new InvalidOperationException("access_token is null");

                // Parse expires_in if present; fallback to 5 minutes
                var expiresInSeconds = 300;
                if (doc.RootElement.TryGetProperty("expires_in", out var expiresEl))
                {
                    if (expiresEl.ValueKind == JsonValueKind.Number)
                    {
                        expiresInSeconds = Math.Max(1, expiresEl.GetInt32());
                    }
                    else if (expiresEl.ValueKind == JsonValueKind.String && int.TryParse(expiresEl.GetString(), out var parsed))
                    {
                        expiresInSeconds = Math.Max(1, parsed);
                    }
                }

                _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
                _cachedAccessToken = token;
                return token;
            }
            finally
            {
                _accessTokenLock.Release();
            }
        }

        private async Task<ClientWebSocket> OpenWebSocketConnectionAsync(string accessToken, CancellationToken ct = default)
        {
            var ws = new ClientWebSocket();
            ws.Options.AddSubProtocol(_webSocketSubprotocol);
            var uri = new Uri($"{_webSocketEndpoint}?accessToken={Uri.EscapeDataString(accessToken)}");
            _logger.Information("Connecting to KONE WS {endpoint}", new Uri(_webSocketEndpoint).GetLeftPart(UriPartial.Path));
            await ws.ConnectAsync(uri, ct).ConfigureAwait(false);
            return ws;
        }

        private object BuildLiftCallPayload(int area, int terminal, int action)
        {
            var targetBuildingId = $"building:{_buildingId}";
            var nowIso = DateTime.UtcNow.ToString("o");
            return new
            {
                type = "lift-call-api-v2",
                buildingId = targetBuildingId,
                callType = "action",
                groupId = _groupId,
                payload = new
                {
                    request_id = GetRequestId(),
                    area = area,
                    time = nowIso,
                    terminal = terminal,
                    call = new
                    {
                        action = action,
                        activate_call_states = new[] { "being_fixed" }
                    }
                }
            };
        }

        private int ResolveAction(string mappingAction)
        {
            if (!string.IsNullOrWhiteSpace(mappingAction))
            {
                if (int.TryParse(mappingAction, out var parsed))
                {
                    return parsed;
                }
            }
            return _defaultAction;
        }

        private static async Task SendWebsocketMessageAsync(ClientWebSocket ws, string text, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        public static async Task<string> ReceiveTextMessageAsync(ClientWebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            using var ms = new MemoryStream();

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", cancellationToken);
                    throw new WebSocketException("WebSocket was closed before a full message was received.");
                }

                ms.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                {
                    // Decode accumulated bytes to UTF8 string
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }

            throw new InvalidOperationException("No message received");
        }

        private static int GetRequestId()
        {
            return RandomNumberGenerator.GetInt32(1, int.MaxValue);
        }
    }
}