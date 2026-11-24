using Kone.Api.Client.Clients.Models;
using Kone.Api.Client.Exceptions;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;
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

        private const string PingTcsId = "ping";

        /// <summary>How long to wait before connecting again when the KONE host name could not be resolved.</summary>
        public static readonly TimeSpan DefaultNameResolutionRetryDelay = TimeSpan.FromSeconds(1);

        public event Action<string>? MessageSend;
        public event Func<string, Task>? MessageReceived;

        private readonly ILogger _log;
        private readonly KoneAuthApiClient _koneAuthApiClient;
        private readonly string _buildingId;
        private readonly string _groupId;
        private readonly string _endpoint;
        private readonly TimeSpan _nameResolutionRetryDelay;
        private readonly HttpMessageInvoker? _webSocketInvoker;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResponse = new();

        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        /// <param name="nameResolutionRetryDelay">How long to wait before connecting once more when the KONE host name
        /// could not be resolved (default 1 s). If the request's token is cancelled while connecting again, the DNS
        /// exception is thrown rather than an OperationCanceledException, because the request was never sent; except
        /// for the call monitor of a call KONE already accepted, which keeps the cancellation.</param>
        /// <param name="webSocketInvoker">Sends the websocket handshake instead of the handler ClientWebSocket creates
        /// itself, for example in tests. The caller keeps it and disposes it.</param>
        public KoneBuildingApiClient(ILogger log,
            KoneAuthApiClient koneAuthApiClient,
            string buildingId,
            string groupId,
            string endpoint = "wss://dev.kone.com/stream-v2",
            TimeSpan? nameResolutionRetryDelay = null,
            HttpMessageInvoker? webSocketInvoker = null)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _koneAuthApiClient = koneAuthApiClient ?? throw new ArgumentNullException(nameof(koneAuthApiClient));
            _buildingId = buildingId ?? throw new ArgumentNullException(nameof(buildingId));
            _groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _nameResolutionRetryDelay = nameResolutionRetryDelay ?? DefaultNameResolutionRetryDelay;
            _webSocketInvoker = webSocketInvoker;

            if (_nameResolutionRetryDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(nameResolutionRetryDelay), "nameResolutionRetryDelay must not be negative");
            }
        }

        public Task<PingResponse> PingAsync(CancellationToken cancellationToken)
        {
            int requestId = GenerateIntRequestId();

            var req = new
            {
                type = LiftCallRequest.ApiTypeCommon,
                buildingId = $"building:{_buildingId}",
                callType = LiftCallRequest.CallTypePing,
                groupId = _groupId,
                payload = new
                {
                    request_id = requestId
                }
            };

            return SendMessageAndWaitForResponseAsync(PingTcsId, req,
                (responseMessage, _) => Task.FromResult(JsonConvert.DeserializeObject<PingResponse>(responseMessage)!),
                cancellationToken);
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
            var requestId = GenerateIntRequestId();

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

        public async Task PlaceLandingCallWithPositionUpdatesAsync(int destinationAreaId,
            Action<string>? positionUpdated,
            bool isDirectionUp, CancellationToken cancellationToken)
        {
            var requestId = GenerateIntRequestId();
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

            await SendMessageAndWaitForResponseAsync(requestId.ToString(), req,
                LandingCallResponseInterceptor,
                cancellationToken);

            return;

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

                try
                {
                    await SendMessageAndWaitForResponseAsync(monitorReqId, monitorReq,
                        (r, _) => Task.FromResult(new LiftCallResponse()),
                        cancellationToken,
                        OnResponseMessage,
                        callAlreadyPlaced: true);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // KONE accepted the call, so the lift was called: only its call states cannot be followed. Anything
                    // but a plain cancellation ends here, so an accepted call is never reported as failed.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _log.Information("KONE accepted the call (session {SessionId}); its call monitor was cancelled", sessionId);
                    }
                    else
                    {
                        _log.Warning(e, "KONE accepted the call (session {SessionId}), but its call monitor failed; its call states are not followed",
                            sessionId);
                    }
                }

                return response;
            }

            void OnResponseMessage(string message)
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                // Call monitor position updates
                if (root.TryGetProperty("requestId", out var requestIdElement) &&
                    requestIdElement.ValueKind == JsonValueKind.String &&
                    requestIdElement.GetString() == monitorReqId &&

                    root.TryGetProperty("callType", out var callTypeElement) &&
                    callTypeElement.ValueKind == JsonValueKind.String &&
                    callTypeElement.GetString() == "monitor" &&

                    root.TryGetProperty("subtopic", out var subTopicElement) &&
                    subTopicElement.ValueKind == JsonValueKind.String)
                {
                    var subTopic = subTopicElement.GetString();

                    if (subTopic != null && subTopic.StartsWith("call_state"))
                    {
                        positionUpdated?.Invoke(message);
                    }
                }
            }
        }

        public Task<LiftCallResponse> PlaceDestinationCallAsync(int sourceAreaId, int destinationAreaId, CancellationToken cancellationToken)
        {
            var requestId = GenerateIntRequestId();

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

        public async Task PlaceDestinationCallWithPositionUpdatesAsync(int sourceAreaId, int destinationAreaId, 
            Action<string>? positionUpdated, CancellationToken cancellationToken)
        {
            var requestId = GenerateIntRequestId();
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
                    area = sourceAreaId,
                    time = DateTime.UtcNow.ToString("o"),
                    call = new Call
                    {
                        action = DestinationCallActionId,
                        destination = destinationAreaId,
                    }
                }
            };

            await SendMessageAndWaitForResponseAsync(requestId.ToString(), req,
            DestinationCallResponseInterceptor,
            cancellationToken);

            return;

            // If a destination call is placed successfully, start monitoring its state changes
            async Task<LiftCallResponse> DestinationCallResponseInterceptor(string message, ClientWebSocket ws)
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

                try
                {
                    await SendMessageAndWaitForResponseAsync(monitorReqId, monitorReq,
                        (r, _) => Task.FromResult(new LiftCallResponse()),
                        cancellationToken,
                        OnResponseMessage,
                        callAlreadyPlaced: true);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // KONE accepted the call, so the lift was called: only its call states cannot be followed. Anything
                    // but a plain cancellation ends here, so an accepted call is never reported as failed.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _log.Information("KONE accepted the call (session {SessionId}); its call monitor was cancelled", sessionId);
                    }
                    else
                    {
                        _log.Warning(e, "KONE accepted the call (session {SessionId}), but its call monitor failed; its call states are not followed",
                            sessionId);
                    }
                }

                return response;
            }

            void OnResponseMessage(string message)
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                // Call monitor position updates
                if (root.TryGetProperty("requestId", out var requestIdElement) &&
                    requestIdElement.ValueKind == JsonValueKind.String &&
                    requestIdElement.GetString() == monitorReqId &&

                    root.TryGetProperty("callType", out var callTypeElement) &&
                    callTypeElement.ValueKind == JsonValueKind.String &&
                    callTypeElement.GetString() == "monitor" &&

                    root.TryGetProperty("subtopic", out var subTopicElement) &&
                    subTopicElement.ValueKind == JsonValueKind.String)
                {
                    var subTopic = subTopicElement.GetString();

                    if (subTopic != null && subTopic.StartsWith("call_state"))
                    {
                        positionUpdated?.Invoke(message);
                    }
                }
            }
        }

        private async Task<TResponse> SendMessageAndWaitForResponseAsync<TResponse>(
            string requestId,
            object request,
            Func<string, ClientWebSocket, Task<TResponse>> responseDeserializerFunc,
            CancellationToken cancellationToken,
            Action<string>? onResponseMessage = null,
            bool callAlreadyPlaced = false)
        {
            ArgumentNullException.ThrowIfNull(requestId);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(responseDeserializerFunc);

            using var ws = await CreatedConnectedWebSocketAsync(cancellationToken, callAlreadyPlaced);

            // Stamp a call when it is really sent (this replaces the time set when the request was built): connecting can
            // take seconds when a DNS lookup is retried
            if (request is LiftCallRequest { payload: { } payload })
            {
                payload.time = DateTime.UtcNow.ToString("o");
            }

            var jsonRequest = SerializeToJson(request);
            var bytes = Encoding.UTF8.GetBytes(jsonRequest);

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

        // Connects once more when the KONE host name could not be resolved. The internal DNS servers of a site can fail
        // the lookup of dev.kone.com now and then, right when its short TTL (60 s) expires, and the next lookup succeeds.
        // Only connecting (the token request and the websocket handshake) is retried, and every request is sent after
        // connecting, so a retry never sends anything twice.
        // If the retry is cancelled (for example by the caller's no-update window), the request was never sent: the DNS
        // failure is rethrown, because a cancellation would read as "KONE did not answer". The call monitor
        // (callAlreadyPlaced) connects after KONE accepted the call, so there the cancellation goes through and the call
        // ends as "no call state", not as a failed call.
        private async Task<ClientWebSocket> CreatedConnectedWebSocketAsync(CancellationToken cancellationToken,
            bool callAlreadyPlaced)
        {
            ExceptionDispatchInfo nameResolutionFailure;

            try
            {
                return await ConnectWebSocketAsync(cancellationToken);
            }
            catch (Exception e) when (IsNameResolutionFailure(e) && !cancellationToken.IsCancellationRequested)
            {
                nameResolutionFailure = ExceptionDispatchInfo.Capture(e);
                _log.Warning("Could not resolve the KONE host ({Error}) for the {Connection}; connecting again in {RetryDelay}",
                    NameResolutionErrorOf(e), callAlreadyPlaced ? "call monitor" : "request", _nameResolutionRetryDelay);
            }

            try
            {
                await Task.Delay(_nameResolutionRetryDelay, cancellationToken);
                return await ConnectWebSocketAsync(cancellationToken);
            }
            catch (Exception e) when (cancellationToken.IsCancellationRequested && IsCancellation(e))
            {
                _log.Debug(e, "Connecting again to KONE was cancelled");

                if (callAlreadyPlaced)
                {
                    _log.Information("KONE accepted the call; stopped connecting its call monitor again: cancelled");
                    throw;
                }

                _log.Information("Stopped connecting again to KONE: cancelled before the request was sent");
                nameResolutionFailure.Throw();
                throw;
            }
        }

        private async Task<ClientWebSocket> ConnectWebSocketAsync(CancellationToken cancellationToken)
        {
            var accessToken = (await _koneAuthApiClient.GetCallGivingAccessTokenAsync(_buildingId,
                _groupId, cancellationToken)).Access_token;

            var webSocket = new ClientWebSocket();
            webSocket.Options.AddSubProtocol("koneapi");
            var uri = new Uri($"{_endpoint}?accessToken={Uri.EscapeDataString(accessToken)}");

            _log.Debug("Connecting to websocket at {Uri}", uri);

            try
            {
                await webSocket.ConnectAsync(uri, _webSocketInvoker, cancellationToken);
            }
            catch
            {
                webSocket.Dispose();
                throw;
            }

            return webSocket;
        }

        // A failed DNS lookup anywhere in the exception chain: the name does not resolve (HostNotFound; NoData on
        // Windows), or the DNS servers gave no usable answer (TryAgain)
        private static bool IsNameResolutionFailure(Exception exception)
        {
            for (Exception? e = exception; e != null; e = e.InnerException)
            {
                if (e is SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData })
                {
                    return true;
                }
            }

            return false;
        }

        // A cancellation anywhere in the exception chain (ClientWebSocket may wrap one)
        private static bool IsCancellation(Exception exception)
        {
            for (Exception? e = exception; e != null; e = e.InnerException)
            {
                if (e is OperationCanceledException)
                {
                    return true;
                }
            }

            return false;
        }

        // The HttpRequestException message names the host, e.g. "Name or service not known (dev.kone.com:443)"
        private static string NameResolutionErrorOf(Exception exception)
        {
            for (Exception? e = exception; e != null; e = e.InnerException)
            {
                if (e is HttpRequestException)
                {
                    return e.Message;
                }
            }

            return exception.GetBaseException().Message;
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

                var callTypePresent = root.TryGetProperty("callType", out var callTypeElement) && callTypeElement.ValueKind == JsonValueKind.String;
                var requestIdPresent = root.TryGetProperty("requestId", out var requestIdElement) && callTypeElement.ValueKind == JsonValueKind.String;
                var subTopicPresent = root.TryGetProperty("subtopic", out var subTopicElement) && callTypeElement.ValueKind == JsonValueKind.String;

                // Handle ping response
                if (callTypePresent && callTypeElement.GetString() == "ping")
                {
                    if (_pendingResponse.TryGetValue(PingTcsId, out var tcs))
                    {
                        tcs.TrySetResult(responseMessage);
                    }
                    return;
                }

                // Handle config response
                if (callTypePresent && requestIdPresent && callTypeElement.GetString() == "config")
                {
                    if (_pendingResponse.TryGetValue(requestIdElement.GetString(), out var tcs))
                    {
                        tcs.TrySetResult(responseMessage);
                    }
                    return;
                }

                // Handle actions response
                if (callTypePresent && requestIdPresent && callTypeElement.GetString() == "actions")
                {
                    if (_pendingResponse.TryGetValue(requestIdElement.GetString(), out var tcs))
                    {
                        tcs.TrySetResult(responseMessage);
                    }
                    return;
                }

                // Handle lift monitor response
                if (callTypePresent && requestIdPresent && callTypeElement.GetString() == "monitor" &&
                    subTopicPresent && subTopicElement.GetString().EndsWith("served"))
                {
                    if (_pendingResponse.TryGetValue(requestIdElement.GetString(), out var tcs))
                    {
                        tcs.TrySetResult(responseMessage);
                    }
                    return;
                }

                // Handle lift monitor response
                if (callTypePresent && requestIdPresent && callTypeElement.GetString() == "monitor" &&
                    subTopicPresent && subTopicElement.GetString().EndsWith("position"))
                {
                    if (_pendingResponse.TryGetValue(requestIdElement.GetString(), out var tcs))
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
        private static int GenerateIntRequestId()
        {
            byte[] bytes = new byte[4];
            Rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF; // Make it positive
        }
    }
}
