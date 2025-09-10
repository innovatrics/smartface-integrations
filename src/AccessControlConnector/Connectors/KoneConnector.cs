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
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private readonly string clientId;
        private readonly string clientSecret;
        private readonly string buildingId;
        private readonly string apiHostname;
        private readonly string webSocketEndpoint;
        private readonly string webSocketSubprotocol;
        private readonly string groupId;
        private readonly int defaultTerminal;
        private readonly int defaultArea;
        private readonly int defaultAction;

        // OAuth token cache
        private string cachedAccessToken;
        private DateTimeOffset accessTokenExpiresAt;
        private readonly SemaphoreSlim accessTokenLock = new SemaphoreSlim(1, 1);
        private readonly TimeSpan accessTokenRefreshSkew = TimeSpan.FromSeconds(60);

        public KoneConnector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            clientId = configuration.GetValue<string>("KoneConfiguration:ClientId") ?? throw new InvalidOperationException("KoneConfiguration:ClientId is required");
            clientSecret = configuration.GetValue<string>("KoneConfiguration:ClientSecret") ?? throw new InvalidOperationException("KoneConfiguration:ClientSecret is required");
            buildingId = configuration.GetValue<string>("KoneConfiguration:BuildingId") ?? throw new InvalidOperationException("KoneConfiguration:BuildingId is required");
            apiHostname = configuration.GetValue<string>("KoneConfiguration:ApiHostname", "dev.kone.com");
            webSocketEndpoint = configuration.GetValue<string>("KoneConfiguration:WebSocketEndpoint", $"wss://{apiHostname}/stream-v2");
            webSocketSubprotocol = configuration.GetValue<string>("KoneConfiguration:WebSocketSubprotocol", "koneapi");
            groupId = configuration.GetValue<string>("KoneConfiguration:GroupId", "1");
            defaultTerminal = configuration.GetValue<int>("KoneConfiguration:Terminal", 1);
            defaultArea = configuration.GetValue<int>("KoneConfiguration:Area", 1000);
            defaultAction = configuration.GetValue<int>("KoneConfiguration:Action", 2001);
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        public async Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            try
            {
                this.logger.Information("KoneConnector OpenAsync: triggering elevator call for stream {streamId}", accessControlMapping?.StreamId);

                using var tokenCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var token = await FetchAccessTokenAsync(tokenCts.Token).ConfigureAwait(false);

                using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var ws = await OpenWebSocketConnectionAsync(token, connectCts.Token).ConfigureAwait(false);

                using var recvCts = new CancellationTokenSource();
                var receiveTask = ReceiveLoopAsync(ws, (msg) => this.logger.Information("KONE WS: {msg}", msg), recvCts.Token);

                var resolvedArea = accessControlMapping?.Area ?? defaultArea;
                var resolvedTerminal = accessControlMapping?.Terminal ?? defaultTerminal;
                var resolvedAction = ResolveAction(accessControlMapping?.Action);
                var targetBuildingId = $"building:{buildingId}";
                this.logger.Information(
                    "KONE call params: buildingId={buildingId}, groupId={groupId}, terminal={terminal}, area={area}, action={action}, streamId={streamId}",
                    targetBuildingId, groupId, resolvedTerminal, resolvedArea, resolvedAction, accessControlMapping?.StreamId);

                var payload = BuildLiftCallPayload(
                    accessControlMapping?.Area ?? defaultArea,
                    accessControlMapping?.Terminal ?? defaultTerminal,
                    ResolveAction(accessControlMapping?.Action)
                );
                var json = JsonSerializer.Serialize(payload);
                using var sendCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await SendTextAsync(ws, json, sendCts.Token).ConfigureAwait(false);

                await Task.Delay(1500).ConfigureAwait(false);

                // Graceful shutdown: signal close and give the receiver a brief moment to drain
                try
                {
                    if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                    {
                        using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", closeCts.Token).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // ignore shutdown errors
                }

                try
                {
                    recvCts.Cancel();
                    await Task.WhenAny(receiveTask, Task.Delay(250)).ConfigureAwait(false);
                }
                catch
                {
                    // ignore drain errors
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "KoneConnector OpenAsync failed");
                throw;
            }
        }

        private async Task<string> FetchAccessTokenAsync(CancellationToken ct = default)
        {
            // Fast path: return cached token if valid (with refresh skew)
            var now = DateTimeOffset.UtcNow;
            var cached = cachedAccessToken;
            if (!string.IsNullOrEmpty(cached) && now < (accessTokenExpiresAt - accessTokenRefreshSkew))
            {
                return cached;
            }

            await accessTokenLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Double-check after acquiring the lock
                now = DateTimeOffset.UtcNow;
                cached = cachedAccessToken;
                if (!string.IsNullOrEmpty(cached) && now < (accessTokenExpiresAt - accessTokenRefreshSkew))
                {
                    return cached;
                }

            var tokenEndpointV2 = $"https://{apiHostname}/api/v2/oauth2/token";
            var scope = $"application/inventory callgiving/group:{buildingId}:{groupId}";

            var httpClient = this.httpClientFactory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpointV2);
            request.Content = new StringContent($"grant_type=client_credentials&scope={Uri.EscapeDataString(scope)}", Encoding.UTF8, "application/x-www-form-urlencoded");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            this.logger.Information("Requesting KONE access token at {endpoint}", tokenEndpointV2);

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

            accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
            cachedAccessToken = token;
            return token;
            }
            finally
            {
                accessTokenLock.Release();
            }
        }

        private async Task<ClientWebSocket> OpenWebSocketConnectionAsync(string accessToken, CancellationToken ct = default)
        {
            var ws = new ClientWebSocket();
            ws.Options.AddSubProtocol(webSocketSubprotocol);
            var uri = new Uri($"{webSocketEndpoint}?accessToken={Uri.EscapeDataString(accessToken)}");
            this.logger.Information("Connecting to KONE WS {endpoint}", new Uri(webSocketEndpoint).GetLeftPart(UriPartial.Path));
            await ws.ConnectAsync(uri, ct).ConfigureAwait(false);
            return ws;
        }

        private object BuildLiftCallPayload(int area, int terminal, int action)
        {
            var targetBuildingId = $"building:{buildingId}";
            var nowIso = DateTime.UtcNow.ToString("o");
            return new
            {
                type = "lift-call-api-v2",
                buildingId = targetBuildingId,
                callType = "action",
                groupId = groupId,
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
            return defaultAction;
        }

        

        private static async Task SendTextAsync(ClientWebSocket ws, string text, CancellationToken ct = default)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }

        private static async Task ReceiveLoopAsync(ClientWebSocket ws, Action<string> onMessage, CancellationToken ct = default)
        {
            var buffer = new byte[8192];
            using var ms = new MemoryStream();
            try
            {
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", ct).ConfigureAwait(false); } catch { }
                            return;
                        }
                        if (result.MessageType != WebSocketMessageType.Text)
                        {
                            continue;
                        }
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    onMessage(Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            catch (OperationCanceledException)
            {
                // normal during shutdown
            }
            catch (WebSocketException)
            {
                // socket closed/aborted during shutdown; ignore
            }
            catch (ObjectDisposedException)
            {
                // socket disposed; ignore
            }
        }

        private static int GetRequestId()
        {
            return RandomNumberGenerator.GetInt32(1, int.MaxValue);
        }
    }
}


