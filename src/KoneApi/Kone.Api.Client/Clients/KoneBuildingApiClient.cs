using Kone.Api.Client.Clients.Models;
using Kone.Api.Client.Exceptions;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Kone.Api.Client.Clients
{
    public class KoneBuildingApiClient : IKoneBuildingApi
    {
        public const int LandingCallUpActionId = 2001;
        public const int LandingCallDownActionId = 2002;

        public const int DestinationCallActionId = 2;

        public event Action<string>? MessageSend;
        public event Func<string, Task>? MessageReceived;

        private readonly ILogger _log;
        private readonly KoneAuthApiClient _koneAuthApiClient;
        private readonly string _buildingId;
        private readonly string _groupId;
        private readonly string _endpoint;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResponse = new();

        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        public KoneBuildingApiClient(ILogger log,
            KoneAuthApiClient koneAuthApiClient,
            string buildingId,
            string groupId,
            string endpoint = "wss://dev.kone.com/stream-v2")
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _koneAuthApiClient = koneAuthApiClient ?? throw new ArgumentNullException(nameof(koneAuthApiClient));
            _buildingId = buildingId ?? throw new ArgumentNullException(nameof(buildingId));
            _groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        public Task<TopologyResponse> GetTopologyAsync(CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString();

            var req = new
            {
                type = CallTypeRequest.ApiTypeCommon,
                requestId,
                buildingId = $"building:{_buildingId}",
                callType = CallTypeRequest.CallTypeConfig,
                groupId = _groupId
            };

            return SendMessageAndWaitForResponseAsync(requestId, req,
                responseMessage => JsonConvert.DeserializeObject<TopologyResponse>(responseMessage)!,
                cancellationToken);
        }

        public Task<ActionsResponse> GetActionsAsync(CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString();

            var req = new
            {
                type = CallTypeRequest.ApiTypeCommon,
                requestId,
                buildingId = $"building:{_buildingId}",
                callType = "actions",
                groupId = _groupId
            };

            return SendMessageAndWaitForResponseAsync(requestId, req,
                responseMessage => JsonConvert.DeserializeObject<ActionsResponse>(responseMessage)!,
                cancellationToken);
        }

        public Task<string> LandingCallAsync(int destinationAreaId, bool isDirectionUp, CancellationToken cancellationToken)
        {
            var requestId = GetRequestId();

            var req = new CallTypeRequest
            {
                type = CallTypeRequest.ApiTypeLiftV2,
                buildingId = $"building:{_buildingId}",
                callType = CallTypeRequest.CallTypeAction,
                groupId = _groupId,
                payload = new Payload
                {
                    request_id = requestId,
                    area = destinationAreaId,
                    time = DateTime.UtcNow.ToString("o"),
                    call = new Call
                    {
                        action = isDirectionUp ? LandingCallUpActionId : LandingCallDownActionId
                    }
                }
            };

            return SendMessageAndWaitForResponseAsync(requestId.ToString(), req,
                MessageParser,
                cancellationToken);

            string MessageParser(string responseMessage)
            {
                using var doc = JsonDocument.Parse(responseMessage);
                var formattedJsonResponse = JsonSerializer.Serialize(doc, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("success", out var success) &&
                    success.ValueKind == JsonValueKind.True)
                {
                    return formattedJsonResponse;
                }

                throw new KoneCallException("No success true found in landing call response", formattedJsonResponse);
            }
        }

        public Task<string> DestinationCallAsync(int sourceAreaId, int destinationAreaId, CancellationToken cancellationToken)
        {
            var requestId = GetRequestId();

            var req = new CallTypeRequest
            {
                type = CallTypeRequest.ApiTypeLiftV2,
                buildingId = $"building:{_buildingId}",
                callType = CallTypeRequest.CallTypeAction,
                groupId = _groupId,
                payload = new Payload
                {
                    request_id = requestId,
                    area = sourceAreaId,
                    time = DateTime.UtcNow.ToString("o"),
                    call = new Call
                    {
                        action = DestinationCallActionId,
                        destination = destinationAreaId,
                    }
                }
            };

            return SendMessageAndWaitForResponseAsync(requestId.ToString(), req,
                MessageParser,
                cancellationToken);

            string MessageParser(string responseMessage)
            {
                using var doc = JsonDocument.Parse(responseMessage);
                var formattedJsonResponse = JsonSerializer.Serialize(doc, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("success", out var success) &&
                    success.ValueKind == JsonValueKind.True)
                {
                    return formattedJsonResponse;
                }

                throw new KoneCallException("No success true found in destination call response", formattedJsonResponse);
            }
        }

        private async Task<TResponse> SendMessageAndWaitForResponseAsync<TResponse>(
            string requestId,
            object request,
            Func<string, TResponse> deserializeFunc,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(requestId);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(deserializeFunc);

            var jsonRequest = SerializeToJson(request);
            var bytes = Encoding.UTF8.GetBytes(jsonRequest);

            using var ws = await CreatedConnectedWebSocketAsync(cancellationToken);

            var tcs = new TaskCompletionSource<string>();
            _pendingResponse.TryAdd(requestId, tcs);

            var cts = new CancellationTokenSource();
            var readingTask = ReceiveMessagesAsync(ws, cts.Token);

            try
            {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);

                string responseMessage = await tcs.Task.WaitAsync(cancellationToken);

                return deserializeFunc(responseMessage);
            }
            finally
            {
                _pendingResponse.TryRemove(requestId, out _);

                try
                {
                    await cts.CancelAsync();
                    await readingTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _log.Error(e, "Failed to cleanup message reading task");
                }
            }
        }


        private async Task<ClientWebSocket> CreatedConnectedWebSocketAsync(CancellationToken cancellationToken)
        {
            var accessToken = (await _koneAuthApiClient.GetCallGivingAccessTokenAsync(_buildingId,
                _groupId, cancellationToken)).Access_token;

            var webSocket = new ClientWebSocket();
            webSocket.Options.AddSubProtocol("koneapi");
            var uri = new Uri($"{_endpoint}?accessToken={Uri.EscapeDataString(accessToken)}");

            _log.Debug("Connecting to websocket at {Uri}", uri);
            await webSocket.ConnectAsync(uri, cancellationToken);
            return webSocket;
        }

        private string SerializeToJson(object request)
        {
            var json = JsonConvert.SerializeObject(request, Formatting.Indented);
            MessageSend?.Invoke(json);
            return json;
        }

        private async Task ReceiveMessagesAsync(ClientWebSocket webSocket, CancellationToken token)
        {
            var buffer = new byte[4096];
            var messageBuffer = new List<byte>();

            while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(buffer, token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", token);
                        return;
                    }

                    messageBuffer.AddRange(buffer.AsSpan(0, result.Count).ToArray());
                }
                while (!result.EndOfMessage);

                var responseMessage = Encoding.UTF8.GetString(messageBuffer.ToArray());
                messageBuffer.Clear();

                HandleMessage(responseMessage);

                if (MessageReceived != null)
                {
                    await MessageReceived.Invoke(responseMessage);
                }
            }

            return;

            void HandleMessage(string responseMessage)
            {
                using var doc = JsonDocument.Parse(responseMessage);
                var root = doc.RootElement;

                var callTypePresent = root.TryGetProperty("callType", out var callType);
                var requestIdPresent = root.TryGetProperty("requestId", out var requestId);

                // Handle config responses
                if (callTypePresent && requestIdPresent && callType.GetString() == "config")
                {
                    if (_pendingResponse.TryGetValue(requestId.GetString(), out var tcs))
                    {
                        tcs.TrySetResult(responseMessage);
                    }
                    return;
                }

                // Handle actions responses
                if (callTypePresent && requestIdPresent && callType.GetString() == "actions")
                {
                    if (_pendingResponse.TryGetValue(requestId.GetString(), out var tcs))
                    {
                        tcs.TrySetResult(responseMessage);
                    }
                    return;
                }

                // Handle lift call responses
                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("request_id", out var reqId) &&
                    reqId.ValueKind == JsonValueKind.Number)
                {
                    if (_pendingResponse.TryGetValue(reqId.GetInt32().ToString(), out var tcs))
                    {
                        tcs.TrySetResult(responseMessage);
                    }
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
    }
}
