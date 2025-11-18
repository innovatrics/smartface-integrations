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
                type = LiftCallRequest.ApiTypeCommon,
                requestId,
                buildingId = $"building:{_buildingId}",
                callType = LiftCallRequest.CallTypeConfig,
                groupId = _groupId
            };

            return SendMessageAndWaitForResponseAsync(requestId, req,
                (responseMessage, _) => Task.FromResult(JsonConvert.DeserializeObject<TopologyResponse>(responseMessage)!),
                cancellationToken);
        }

        public Task<ActionsResponse> GetActionsAsync(CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString();

            var req = new
            {
                type = LiftCallRequest.ApiTypeCommon,
                requestId,
                buildingId = $"building:{_buildingId}",
                callType = "actions",
                groupId = _groupId
            };

            return SendMessageAndWaitForResponseAsync(requestId, req,
                (responseMessage, _) => Task.FromResult(JsonConvert.DeserializeObject<ActionsResponse>(responseMessage)!),
                cancellationToken);
        }

        public Task<LiftPositionResponse> GetLiftPositionAsync(int liftId, CancellationToken cancellationToken)
        {
            var monitorReqId = Guid.NewGuid().ToString();

            var liftMonitorReq = new
            {
                type = LiftCallRequest.ApiTypeSiteMonitoring,
                requestId = monitorReqId,
                buildingId = $"building:{_buildingId}",
                callType = LiftCallRequest.CallTypeMonitor,
                groupId = _groupId,
                payload = new
                {
                    sub = $"DeckPosition_{Guid.NewGuid()}",
                    duration = 10,
                    subtopics = new[]
                    {
                        $"lift_{liftId}/position",
                    }
                }
            };

            return SendMessageAndWaitForResponseAsync(monitorReqId, liftMonitorReq,
                (responseMessage, _) => Task.FromResult(JsonConvert.DeserializeObject<LiftPositionResponse>(responseMessage)!),
                cancellationToken);
        }

        public Task<LiftCallResponse> PlaceLandingCallAsync(int destinationAreaId,
            bool isDirectionUp, CancellationToken cancellationToken)
        {
            var requestId = GetRequestId();

            var req = new LiftCallRequest
            {
                type = LiftCallRequest.ApiTypeLiftV2,
                buildingId = $"building:{_buildingId}",
                callType = LiftCallRequest.CallTypeAction,
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
                LiftCallResponseMessageParser,
                cancellationToken);
        }

        public Task<LiftCallResponse> PlaceLandingCallWithPositionUpdatesAsync(int destinationAreaId,
            Action<string>? positionUpdated,
            bool isDirectionUp, CancellationToken cancellationToken)
        {
            var requestId = GetRequestId();
            var monitorReqId = Guid.NewGuid().ToString();

            var req = new LiftCallRequest
            {
                type = LiftCallRequest.ApiTypeLiftV2,
                buildingId = $"building:{_buildingId}",
                callType = LiftCallRequest.CallTypeAction,
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
                LandingCallResponseInterceptor,
                cancellationToken,
                OnResponseMessage);

            void OnResponseMessage(string message)
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                // Call monitor position updates
                if (root.TryGetProperty("requestId", out var requestId) &&
                    requestId.GetString() == monitorReqId &&

                    root.TryGetProperty("callType", out var callType) &&
                    callType.GetString() == "monitor" &&

                    root.TryGetProperty("subtopic", out var subTopic) &&
                    subTopic.GetString().EndsWith("position"))
                {
                    positionUpdated?.Invoke(message);
                }
            }

            // If a landing call is placed successfully, start monitoring its state changes
            async Task<LiftCallResponse> LandingCallResponseInterceptor(string message, ClientWebSocket ws)
            {
                var response = await LiftCallResponseMessageParser(message, ws);
                var sessionId = response.data.session_id;

                //https://dev.kone.com/api-portal/dashboard/api-documentation/elevator-websocket-api-v2#monitoring-commands
                var monitorReq = new
                {
                    type = LiftCallRequest.ApiTypeSiteMonitoring,
                    requestId = monitorReqId,
                    buildingId = $"building:{_buildingId}",
                    callType = LiftCallRequest.CallTypeMonitor,
                    groupId = _groupId,
                    payload = new
                    {
                        sub = $"LandingCall_{Guid.NewGuid()}",
                        duration = 300,
                        subtopics = new[]
                        {
                            $"call_state/{sessionId}/registered",
                            $"call_state/{sessionId}/being_assigned",
                            $"call_state/{sessionId}/assigned",
                            $"call_state/{sessionId}/being_fixed",
                            $"call_state/{sessionId}/fixed",
                            $"call_state/{sessionId}/served",
                            $"call_state/{sessionId}/cancelled"
                        }
                    }
                };

                await SendMessageAndWaitForResponseAsync(monitorReqId, monitorReq,
                    (r, _) => Task.FromResult(new LiftCallResponse()), cancellationToken);

                return response;
            }
        }

        public Task<LiftCallResponse> PlaceDestinationCallAsync(int sourceAreaId, int destinationAreaId, CancellationToken cancellationToken)
        {
            var requestId = GetRequestId();

            var req = new LiftCallRequest
            {
                type = LiftCallRequest.ApiTypeLiftV2,
                buildingId = $"building:{_buildingId}",
                callType = LiftCallRequest.CallTypeAction,
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
                LiftCallResponseMessageParser,
                cancellationToken);
        }

        private async Task<TResponse> SendMessageAndWaitForResponseAsync<TResponse>(
            string requestId,
            object request,
            Func<string, ClientWebSocket, Task<TResponse>> responseDeserializerFunc,
            CancellationToken cancellationToken,
            Action<string>? onResponseMessage = null)
        {
            ArgumentNullException.ThrowIfNull(requestId);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(responseDeserializerFunc);

            var jsonRequest = SerializeToJson(request);
            var bytes = Encoding.UTF8.GetBytes(jsonRequest);

            using var ws = await CreatedConnectedWebSocketAsync(cancellationToken);

            var tcs = new TaskCompletionSource<string>();
            _pendingResponse.TryAdd(requestId, tcs);

            var cts = new CancellationTokenSource();
            var readingTask = ReceiveMessagesAsync(ws, onResponseMessage, cts.Token);

            try
            {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);

                var responseMessage = await tcs.Task.WaitAsync(cancellationToken);

                return await responseDeserializerFunc(responseMessage, ws);
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


        private static Task<LiftCallResponse> LiftCallResponseMessageParser(string responseMessage, ClientWebSocket clientWebSocket)
        {
            var response = JsonSerializer.Deserialize<LiftCallResponse>(responseMessage);

            if (response == null)
            {
                throw new KoneCallException("Failed to deserialize call response", responseMessage);
            }

            if (!response.data.success)
            {
                throw new KoneCallException("No success true found in call response", responseMessage);
            }

            response.ResponseMessageRaw = responseMessage;

            return Task.FromResult(response);
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

        private async Task ReceiveMessagesAsync(WebSocket webSocket, Action<string> onResponseMessage, CancellationToken token)
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

                onResponseMessage?.Invoke(responseMessage);
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
                var subTopicPresent = root.TryGetProperty("subtopic", out var subTopic);

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

                // Handle lift monitor responses
                if (callTypePresent && requestIdPresent && callType.GetString() == "monitor" &&
                    subTopicPresent && subTopic.GetString().EndsWith("served"))
                {
                    if (_pendingResponse.TryGetValue(requestId.GetString(), out var tcs))
                    {
                        tcs.TrySetResult(responseMessage);
                    }
                    return;
                }

                // Handle lift monitor responses
                if (callTypePresent && requestIdPresent && callType.GetString() == "monitor" &&
                    subTopicPresent && subTopic.GetString().EndsWith("position"))
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
                    return;
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
