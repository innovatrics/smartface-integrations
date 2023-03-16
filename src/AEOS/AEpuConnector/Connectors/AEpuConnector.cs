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

        private async Task<Socket> OpenSocketAsync(string aepuHostname, int aepuPort)
        {
            Socket newSocket = null;
            
            try
            {
                newSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                await newSocket.ConnectAsync(aepuHostname,aepuPort);
                this.logger.Information($"Socket connected to -> {aepuHostname}:{aepuPort} ");
            }
            catch (Exception ex)
            {
                newSocket?.Shutdown(SocketShutdown.Both);
                newSocket?.Dispose();
                this.logger.Error(ex,$"Failed to Connect to Socket {aepuHostname}:{aepuPort}.");
                throw;
            }
            
            return newSocket;
        }

        public async Task OpenAsync(string aepuHostname, int aepuPort, string watchlistMemberID)
        {
            this.logger.Information("Sending ipBadge to {AEpuHostname}:{AEpuPort} for user {WatchlistMemberID}", aepuHostname, aepuPort, watchlistMemberID);

            try
            {

                if(socket == null)
                {
                    socket = await OpenSocketAsync(aepuHostname,aepuPort);
                }

                try
                {                   

                    var clientId = watchlistMemberID;
                    var encodedClientId = Encoding.UTF8.GetBytes(clientId);

                    if (encodedClientId.Length > 28)
                        throw new ApplicationException("Client ID is too long");

                    var messageBytes = new byte[2 + encodedClientId.Length + 2];
                    messageBytes[0] = 0x02;
                    messageBytes[1] = (byte)encodedClientId.Length;
                    for (int i = 0; i < encodedClientId.Length; i++)
                    {
                        messageBytes[2 + i] = encodedClientId[i];
                    }

                    byte checksum = 0;

                    for (int i = 0; i < encodedClientId.Length + 1; i++)
                    {
                        checksum = (byte)(checksum ^ messageBytes[1 + i]);
                    }

                    messageBytes[messageBytes.Length - 2] = checksum;
                    messageBytes[messageBytes.Length - 1] = 0x03;

                    int byteSent = socket.Send(messageBytes);

                    byte[] messageReceived = new byte[1024];

                    int byteRecv = socket.Receive(messageReceived);
                    this.logger.Information("Message from Server -> {0}", Encoding.ASCII.GetString(messageReceived, 0, byteRecv));

                }
                catch (ArgumentNullException ane)
                {
                    this.logger.Error("ArgumentNullException : {0}", ane.ToString());
                }
                catch (SocketException se)
                {
                    this.logger.Error("SocketException : {0}", se.ToString());
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