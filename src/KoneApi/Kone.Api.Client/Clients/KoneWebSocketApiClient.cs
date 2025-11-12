using Kone.Api.Client.Clients.Models;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kone.Api.Client.Clients
{
    public class KoneWebSocketApiClient : IAsyncDisposable
    {
        public event Action<string>? MessageSend;
        public event Func<string, Task>? MessageReceived;

        private readonly ILogger _log;
        private readonly KoneApiClient _koneApiClient;
        private readonly string _buildingId;
        private readonly string _groupId;
        private readonly string _endpoint;
        private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5);
        private ClientWebSocket _webSocket;

        private readonly Task _messageReadingTask;
        private readonly CancellationTokenSource _messageReadingCts = new();

        private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _pendingResponse = new();

        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        public KoneWebSocketApiClient(ILogger log,
            KoneApiClient koneApiClient,
            string buildingId,
            string groupId,
            string endpoint = "wss://dev.kone.com/stream-v2")
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _koneApiClient = koneApiClient ?? throw new ArgumentNullException(nameof(koneApiClient));
            _buildingId = buildingId ?? throw new ArgumentNullException(nameof(buildingId));
            _groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

            _messageReadingTask = StartReadingMessagesAsync(_messageReadingCts.Token);
        }

        public async Task<string> CallLiftToAreaAsync(int landingAreaId, CancellationToken cancellationToken)
        {
            var requestId = GetRequestId();

            var req = new CallTypeRequest
            {
                type = "lift-call-api-v2",
                callType = CallTypeRequest.CallTypeAction,
                buildingId = $"building:{_buildingId}",
                groupId = _groupId,
                payload = new Payload
                {
                    request_id = requestId,
                    time = DateTime.UtcNow.ToString("o"),
                    call = new Call
                    {
                        action = 999,//Call.LandingCallDown, // TODO: Does up or down matters ?
                    },
                    area = landingAreaId
                }
            };

            var jsonMessage = SerializeToJson(req);
            var bytes = Encoding.UTF8.GetBytes(jsonMessage);

            await EnsureSocketConnectedAsync(cancellationToken);

            var tcs = new TaskCompletionSource<string>();
            _pendingResponse.TryAdd(requestId, tcs);

            try
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);

                string responseMessage = await tcs.Task.WaitAsync(cancellationToken);

                using var doc = JsonDocument.Parse(responseMessage);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("success", out var success) &&
                    success.ValueKind == JsonValueKind.True)
                {
                    return responseMessage;
                }
                else
                {
                    throw new KoneLiftCallException("No success true found in response message", responseMessage);
                }
            }
            finally
            {
                _pendingResponse.TryRemove(requestId, out _);
            }
        }

        public async Task CallLiftToDestinationAsync(int area, int terminal, CancellationToken cancellationToken)
        {
            var req = new CallTypeRequest
            {
                callType = CallTypeRequest.CallTypeAction,
                buildingId = $"building:{_buildingId}",
                groupId = _groupId,
                type = "lift-call-api-v2",
                payload = new Payload
                {
                    request_id = GetRequestId(),
                    time = DateTime.UtcNow.ToString("o"),
                    call = new Call
                    {
                        action = Call.ElevatorCarCall,
                        destination = 3000
                    },
                    area = area,
                    terminal = terminal
                }
            };

            var jsonMessage = SerializeToJson(req);
            var bytes = Encoding.UTF8.GetBytes(jsonMessage);

            await EnsureSocketConnectedAsync(cancellationToken);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task<string> GetAccessTokenAsync(string buildingId, string groupId)
        {
            var scope = $"application/inventory callgiving/group:{buildingId}:{groupId}";
            var tokenResponse = await _koneApiClient.GetAccessTokenAsync(scope);
            return tokenResponse.Access_token;
        }

        private async Task StartReadingMessagesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string accessToken;

                try
                {
                    accessToken = await GetAccessTokenAsync(_buildingId, _groupId);
                }
                catch (Exception e)
                {
                    _log.Error(e, "Failed to fetch access token");
                    await Task.Delay(_reconnectDelay, cancellationToken);
                    continue;
                }

                try
                {
                    _webSocket = new ClientWebSocket();
                    _webSocket.Options.AddSubProtocol("koneapi");

                    var uri = new Uri($"{_endpoint}?accessToken={Uri.EscapeDataString(accessToken)}");
                    _log.Information("Connecting to websocket at {Uri}", uri);
                    await _webSocket.ConnectAsync(uri, cancellationToken);

                    await ReceiveLoopAsync(_webSocket, cancellationToken);
                }
                catch (WebSocketException wex) when (wex.Message.Contains("401"))
                {
                    _log.Warning("Websocket connection returned unauthorized");
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Websocket connection error");
                }
                finally
                {
                    _log.Information("Reconnecting after {Delay}", _reconnectDelay);
                    await Task.Delay(_reconnectDelay, cancellationToken);
                }
            }
        }

        private async Task EnsureSocketConnectedAsync(CancellationToken cancellationToken)
        {
            if (_webSocket is null or { State: WebSocketState.None } or { State: WebSocketState.Connecting })
            {
                // Give ~2s time for the websocket connection to establish
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(200, cancellationToken);

                    if (_webSocket is { State: WebSocketState.Open })
                    {
                        return;
                    }
                }
            }

            if (_webSocket is not { State: WebSocketState.Open })
            {
                throw new InvalidOperationException("Websocket is not connected yet");
            }
        }

        private string SerializeToJson(object request)
        {
            var json = JsonConvert.SerializeObject(request, Formatting.Indented);
            MessageSend?.Invoke(json);
            return json;
        }

        private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
        {
            var buffer = new byte[4096];

            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var responseMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

                using var doc = JsonDocument.Parse(responseMessage);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("request_id", out var reqId) &&
                    reqId.ValueKind == JsonValueKind.Number)
                {
                    if (_pendingResponse.TryGetValue(reqId.GetInt32(), out var tcs))
                    {
                        tcs.TrySetResult(responseMessage);
                    }
                }

                if (MessageReceived == null)
                {
                    continue;
                }

                try
                {
                    await MessageReceived.Invoke(responseMessage);
                }
                catch (Exception e)
                {
                    _log.Error(e, "Message handler failed");
                }
            }
        }

        // Generates a positive 32-bit integer (1 to 2,147,483,647)
        private static int GetRequestId()
        {
            byte[] bytes = new byte[4];
            Rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF; // Make it positive
        }

        public async ValueTask DisposeAsync()
        {
            await _messageReadingCts.CancelAsync();

            try
            {
                await _messageReadingTask;
            }
            catch (OperationCanceledException) { }
        }
    }
}
