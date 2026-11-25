using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kone.Api.Client.Tests
{
    /// <summary>
    /// A local websocket server that answers like the KONE elevator websocket API, enough for KoneBuildingApiClient:
    /// a ping, a landing call (accepted) and a call monitor (call states registered, then served). No TLS, no auth.
    /// </summary>
    internal sealed class FakeKoneServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _accepting;
        private int _landingCalls;
        private int _monitors;
        private string? _lastLandingCallTime;

        public FakeKoneServer()
        {
            _listener.Start();
            _accepting = AcceptAsync();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public string Endpoint => $"ws://127.0.0.1:{Port}/stream-v2";

        public int LandingCalls => Volatile.Read(ref _landingCalls);

        public int Monitors => Volatile.Read(ref _monitors);

        /// <summary>The payload.time of the last landing call received.</summary>
        public string? LastLandingCallTime => Volatile.Read(ref _lastLandingCallTime);

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            await _accepting;
            _stop.Dispose();
        }

        private async Task AcceptAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => ServeAsync(client));
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var key = await ReadWebSocketKeyAsync(stream);
                    var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(
                        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                        $"Sec-WebSocket-Accept: {accept}\r\nSec-WebSocket-Protocol: koneapi\r\n\r\n"), _stop.Token);

                    using var ws = WebSocket.CreateFromStream(stream,
                        new WebSocketCreationOptions { IsServer = true, SubProtocol = "koneapi" });
                    var buffer = new byte[4096];
                    var message = new List<byte>();

                    while (ws.State == WebSocketState.Open)
                    {
                        var result = await ws.ReceiveAsync(buffer, _stop.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        message.AddRange(buffer.AsSpan(0, result.Count).ToArray());
                        if (!result.EndOfMessage)
                        {
                            continue;
                        }

                        var request = Encoding.UTF8.GetString(message.ToArray());
                        message.Clear();

                        foreach (var reply in RepliesTo(request))
                        {
                            await ws.SendAsync(Encoding.UTF8.GetBytes(reply), WebSocketMessageType.Text, true, _stop.Token);
                        }
                    }
                }
                catch (Exception)
                {
                    // The client went away without a close handshake (it simply disposes its socket), or the server stopped
                }
            }
        }

        private List<string> RepliesTo(string request)
        {
            using var doc = JsonDocument.Parse(request);
            var root = doc.RootElement;

            switch (root.GetProperty("callType").GetString())
            {
                case "ping":
                    return ["{\"callType\":\"ping\",\"data\":{}}"];

                case "action":
                    Interlocked.Increment(ref _landingCalls);
                    Volatile.Write(ref _lastLandingCallTime, root.GetProperty("payload").GetProperty("time").GetString());
                    var requestId = root.GetProperty("payload").GetProperty("request_id").GetInt32();
                    return ["{\"data\":{\"request_id\":" + requestId + ",\"session_id\":42,\"success\":true}}"];

                case "monitor":
                    Interlocked.Increment(ref _monitors);
                    var monitorId = root.GetProperty("requestId").GetString();
                    return [CallState(monitorId!, "registered"), CallState(monitorId!, "served")];

                default:
                    return [];
            }
        }

        private static string CallState(string monitorId, string state) =>
            "{\"callType\":\"monitor\",\"requestId\":\"" + monitorId + "\",\"subtopic\":\"call_state/42/" + state + "\",\"data\":{}}";

        private async Task<string> ReadWebSocketKeyAsync(NetworkStream stream)
        {
            var header = new StringBuilder();
            var one = new byte[1];

            while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                if (await stream.ReadAsync(one, _stop.Token) == 0)
                {
                    throw new IOException("The client closed the connection during the handshake");
                }

                header.Append((char)one[0]);
            }

            foreach (var line in header.ToString().Split("\r\n"))
            {
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                {
                    return line["Sec-WebSocket-Key:".Length..].Trim();
                }
            }

            throw new InvalidOperationException("Not a websocket upgrade request");
        }
    }

    /// <summary>
    /// The connect step of the websocket handshake, for KoneBuildingApiClient's webSocketInvoker. The n-th attempt fails
    /// with the n-th given SocketError; later attempts connect to the fake KONE whatever host the endpoint names.
    /// SocketsHttpHandler wraps a failure the way production sees it: WebSocketException > HttpRequestException
    /// (NameResolutionError for HostNotFound, "... (host:port)") > SocketException.
    /// </summary>
    internal sealed class FakeHandshake : IDisposable
    {
        public const string Endpoint = "ws://kone.test/stream-v2";

        private readonly SocketError[] _failures;
        private int _attempts;

        public FakeHandshake(FakeKoneServer kone, params SocketError[] failures)
        {
            _failures = failures;
            Invoker = new HttpMessageInvoker(new SocketsHttpHandler
            {
                UseProxy = false,
                ConnectCallback = async (_, ct) =>
                {
                    var n = Interlocked.Increment(ref _attempts);
                    if (n <= _failures.Length)
                    {
                        throw new SocketException((int)_failures[n - 1]);
                    }

                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(IPAddress.Loopback, kone.Port, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            });
        }

        public HttpMessageInvoker Invoker { get; }

        public int Attempts => Volatile.Read(ref _attempts);

        public void Dispose() => Invoker.Dispose();
    }

    /// <summary>
    /// Answers the OAuth token requests of KoneAuthApiClient without a network. The n-th request runs the n-th behaviour;
    /// the last one serves every later request.
    /// </summary>
    internal sealed class FakeTokenHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>>[] _behaviours;
        private int _requests;

        public FakeTokenHandler(params Func<CancellationToken, Task<HttpResponseMessage>>[] behaviours)
        {
            _behaviours = behaviours;
        }

        public int Requests => Volatile.Read(ref _requests);

        public static Task<HttpResponseMessage> Token(CancellationToken _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"t\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"s\"}",
                    Encoding.UTF8, "application/json")
            });

        // What SocketsHttpHandler throws on Linux when the host name does not resolve
        public static Task<HttpResponseMessage> NameNotResolved(CancellationToken _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException(HttpRequestError.NameResolutionError,
                "Name or service not known (dev.kone.com:443)", new SocketException((int)SocketError.HostNotFound)));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _requests) - 1;
            return _behaviours[Math.Min(n, _behaviours.Length - 1)](cancellationToken);
        }
    }
}
