using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors
{
    public class AeosConnector : IAccessControlConnector
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private Socket _socket;

        public AeosConnector(ILogger logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        private async Task<Socket> CreateOpenSocketAsync(string host, int port)
        {
            try
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(host, port);

                _logger.Information($"Socket connected to {host}:{port}");

                return socket;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Socket failed to connect to {host}:{port}");
                throw ex;
            }
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        public async Task OpenAsync(StreamConfig streamConfig, string accessControlUserId = null)
        {
            _logger.Information($"Sending ipBadge to {streamConfig.Host}:{streamConfig.Port}");

            try
            {
                if (_socket == null)
                {
                    _socket = await CreateOpenSocketAsync(streamConfig.Host, streamConfig.Port ?? 0);
                }
                var clientIdBytes = Encoding.UTF8.GetBytes(accessControlUserId);

                // double check if the clientId is in the correct format
                if (clientIdBytes.Length > 28 || clientIdBytes.Length < 1)
                {
                    _logger.Debug($"{nameof(accessControlUserId)} converted to byte[] must be in range of 1 to 28 bytes, current length: {clientIdBytes.Length}");
                    throw new ArgumentException("ClientID must be in range of 1 to 28 bytes");
                }

                var messageBytes = new byte[2 + clientIdBytes.Length + 2];
                messageBytes[0] = 0x02;
                messageBytes[1] = (byte)clientIdBytes.Length;
                for (int i = 0; i < clientIdBytes.Length; i++)
                {
                    messageBytes[2 + i] = clientIdBytes[i];
                }

                byte checksum = 0;

                for (int i = 0; i < clientIdBytes.Length + 1; i++)
                {
                    checksum = (byte)(checksum ^ messageBytes[1 + i]);
                }

                messageBytes[messageBytes.Length - 2] = checksum;
                messageBytes[messageBytes.Length - 1] = 0x03;

                try
                {
                    _logger.Debug($"Sending {messageBytes.Length} bytes to socket");

                    var bytesSent = _socket.Send(messageBytes);

                    _logger.Debug($"Sent {bytesSent} bytes");

                    var messageReceived = new byte[1024];

                    _logger.Debug($"Reading from socket");

                    var bytesReceived = _socket.Receive(messageReceived);

                    _logger.Debug($"Read {bytesReceived} bytes from socket");

                    _logger.Information("Message from socket {0}", Encoding.ASCII.GetString(messageReceived, 0, bytesReceived));
                }
                catch (SocketException se)
                {
                    _logger.Error($"SocketException: {se.Message}, Code {se.SocketErrorCode}");
                    _socket?.Shutdown(SocketShutdown.Both);
                    _socket?.Dispose();
                    _socket = null;
                }
                catch (Exception e)
                {
                    _logger.Error("Unexpected exception : {0}", e.ToString());
                }

            }

            catch (Exception e)
            {
                _logger.Error(e.ToString());
            }

        }

    }
}