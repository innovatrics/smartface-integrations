using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors
{
    public class KoneConnector : IAccessControlConnector
    {
        private readonly ILogger _log;
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
            _log = logger ?? throw new ArgumentNullException(nameof(logger));
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
                _log.Information("Triggering KONE elevator call for stream {StreamId}", accessControlMapping?.StreamId);

                using var fetchTokenCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var token = await FetchAccessTokenAsync(fetchTokenCts.Token);

                using var wsCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var ws = await OpenWebSocketConnectionAsync(token, wsCts.Token);

                var resolvedArea = accessControlMapping?.Area ?? _defaultArea;
                var resolvedTerminal = accessControlMapping?.Terminal ?? _defaultTerminal;
                var resolvedAction = ResolveAction(accessControlMapping?.Action);

                var reqId = GetRequestId();
                var payload = BuildLiftCallPayload(resolvedArea, resolvedTerminal, resolvedAction, reqId);

                _log.Information("Payload is {@Payload}", payload);

                using var messageSendCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var messageTask = ReceiveResponseMessageAsync(ws, reqId, messageSendCts.Token);

                await SendWebsocketMessageAsync(ws, JsonSerializer.Serialize(payload), messageSendCts.Token);

                var message = await messageTask;

                _log.Information("KONE response messages: {Message}", message);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Triggering KONE elevator call for stream {StreamId} failed", accessControlMapping?.StreamId);
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

                _log.Information("Requesting KONE access token at {endpoint}", tokenEndpointV2);

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
            _log.Information("Connecting to KONE WS {endpoint}", new Uri(_webSocketEndpoint).GetLeftPart(UriPartial.Path));
            await ws.ConnectAsync(uri, ct).ConfigureAwait(false);
            return ws;
        }

        private object BuildLiftCallPayload(int area, int terminal, int action, int requestId)
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
                    request_id = requestId,
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

        private async Task<Dictionary<string, object>> ReceiveResponseMessageAsync(ClientWebSocket ws, int requestId, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string responseString;
                try
                {
                    responseString = await ReceiveMessageAsync(ws, cancellationToken);
                }
                catch (OperationCanceledException oce)
                {
                    throw new TimeoutException($"No message matching request id {requestId} received", oce);
                }

                var responseMessage = JsonSerializer.Deserialize<Dictionary<string, object>>(responseString);

                using (var doc = JsonDocument.Parse(responseString))
                {
                    var root = doc.RootElement;

                    if (!TryExtractRequestId(root, out var respId))
                    {
                        _log.Information("Received message without request id {@Message}", responseMessage);
                        continue;
                    }

                    if (respId != requestId)
                    {
                        continue;
                    }

                    _log.Information("Received response message {@Message} matching request id {RequestId}", responseMessage, requestId);

                    if (!TryExtractSuccess(root, out var isSuccess))
                    {
                        // Not a final response (e.g., initial ack). Keep waiting.
                        continue;
                    }

                    if (isSuccess)
                    {
                        return responseMessage;
                    }

                    throw new InvalidOperationException($"KONE response did not signal success. Message: {responseString}");
                }
            }

            throw new TimeoutException($"No message matching request id {requestId} received");

            static bool TryExtractRequestId(JsonElement root, out int extractedRequestId)
            {
                extractedRequestId = default;

                // Top-level snake_case: { "request_id": 123 }
                if (root.TryGetProperty("request_id", out var ridSnake)
                    && ridSnake.ValueKind == JsonValueKind.Number
                    && ridSnake.TryGetInt32(out extractedRequestId))
                {
                    return true;
                }

                // Top-level camelCase: { "requestId": 123 }
                if (root.TryGetProperty("requestId", out var ridCamel)
                    && ridCamel.ValueKind == JsonValueKind.Number
                    && ridCamel.TryGetInt32(out extractedRequestId))
                {
                    return true;
                }

                // Nested in data: { "data": { "request_id": 123, ... } }
                if (root.TryGetProperty("data", out var data)
                    && data.ValueKind == JsonValueKind.Object
                    && data.TryGetProperty("request_id", out var ridData)
                    && ridData.ValueKind == JsonValueKind.Number
                    && ridData.TryGetInt32(out extractedRequestId))
                {
                    return true;
                }

                return false;
            }

            static bool TryExtractSuccess(JsonElement root, out bool success)
            {
                success = default;

                // Top-level: { "success": true/false }
                if (root.TryGetProperty("success", out var sTop))
                {
                    if (sTop.ValueKind == JsonValueKind.True) { success = true; return true; }
                    if (sTop.ValueKind == JsonValueKind.False) { success = false; return true; }
                }

                // Nested: { "data": { "success": true/false } }
                if (root.TryGetProperty("data", out var data)
                    && data.ValueKind == JsonValueKind.Object
                    && data.TryGetProperty("success", out var sNested))
                {
                    if (sNested.ValueKind == JsonValueKind.True) { success = true; return true; }
                    if (sNested.ValueKind == JsonValueKind.False) { success = false; return true; }
                }

                return false;
            }

            static async Task<string> ReceiveMessageAsync(ClientWebSocket ws, CancellationToken cancellationToken)
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
                        var responseString = Encoding.UTF8.GetString(ms.ToArray());
                        return responseString;
                    }
                }
                throw new InvalidOperationException("No message received");
            }
        }

        private static int GetRequestId()
        {
            return RandomNumberGenerator.GetInt32(1, int.MaxValue);
        }
    }
}