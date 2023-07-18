using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AEpuConnector.Connectors
{
    public class AEpuConnector : IAEpuConnector
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private Socket socket;

        public AEpuConnector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            this.socket = null;
        }

        private async Task<Socket> CreateOpenSocketAsync(string aepuHostname, int aepuPort)
        {
            try
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(aepuHostname, aepuPort);

                this.logger.Information($"Socket connected to {aepuHostname}:{aepuPort}");

                return socket;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"Socket failed to connect to {aepuHostname}:{aepuPort}");
                throw ex;
            }
        }

        public async Task OpenAsync(string aepuHostname, int aepuPort, byte[] clientId)
        {
            this.logger.Information("Sending ipBadge to {AEpuHostname}:{AEpuPort}", aepuHostname, aepuPort);

            try
            {
                if (socket == null)
                {
                    socket = await CreateOpenSocketAsync(aepuHostname, aepuPort);
                }

                if (clientId.Length > 28 || clientId.Length < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(clientId), clientId, "ClientID must be in range of 1 to 28 bytes");
                }

                var messageBytes = new byte[2 + clientId.Length + 2];
                messageBytes[0] = 0x02;
                messageBytes[1] = (byte)clientId.Length;
                for (int i = 0; i < clientId.Length; i++)
                {
                    messageBytes[2 + i] = clientId[i];
                }

                byte checksum = 0;

                for (int i = 0; i < clientId.Length + 1; i++)
                {
                    checksum = (byte)(checksum ^ messageBytes[1 + i]);
                }

                messageBytes[messageBytes.Length - 2] = checksum;
                messageBytes[messageBytes.Length - 1] = 0x03;

                try
                {
                    this.logger.Debug($"Sending {messageBytes.Length} bytes to socket");

                    var bytesSent = socket.Send(messageBytes);

                    this.logger.Debug($"Sent {bytesSent} bytes");

                    var messageReceived = new byte[1024];

                    this.logger.Debug($"Reading from socket");

                    var bytesReceived = socket.Receive(messageReceived);

                    this.logger.Debug($"Read {bytesReceived} bytes from socket");

                    this.logger.Information("Message from socket {0}", Encoding.ASCII.GetString(messageReceived, 0, bytesReceived));
                }
                catch (SocketException se)
                {
                    this.logger.Error($"SocketException: {se.Message}, Code {se.SocketErrorCode}");
                    socket?.Shutdown(SocketShutdown.Both);
                    socket?.Dispose();
                    socket = null;
                }
                catch (Exception e)
                {
                    this.logger.Error("Unexpected exception : {0}", e.ToString());
                }

            }

            catch (Exception e)
            {
                this.logger.Error(e.ToString());
            }

        }

    }
}