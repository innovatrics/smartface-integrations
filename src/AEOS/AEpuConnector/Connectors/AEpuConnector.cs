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

        private System.Net.IPAddress ipAddr;

        private System.Net.IPEndPoint localEndPoint;

        public AEpuConnector(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(string AEpuHostname, int AEpuPort, string WatchlistMemberID)
        {
            this.logger.Information("Sending ipBadge to {AEpuHostname}:{AEpuPort} for user {WatchlistMemberID}", AEpuHostname, AEpuPort, WatchlistMemberID);

            try
            {

                if (Dns.GetHostAddresses(AEpuHostname).Length == 0)
                {
                    throw new ArgumentException(
                        "Unable to retrieve address from specified host name.",
                        "hostName"
                    );
                }
                else
                {
                    System.Net.IPHostEntry ipHost = Dns.GetHostEntry(AEpuHostname);
                    ipAddr = ipHost.AddressList[0];
                    localEndPoint = new System.Net.IPEndPoint(ipAddr, AEpuPort);

                    this.logger.Information("IP Address Received for the hostname {AEpuHostname}: {ipAddr}", AEpuHostname, ipAddr);
                }

                var sender = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try
                {

                    sender.Connect(localEndPoint);

                    this.logger.Information("Socket connected to -> {0} ", sender.RemoteEndPoint.ToString());

                    var clientId = WatchlistMemberID;
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

                    int byteSent = sender.Send(messageBytes);

                    byte[] messageReceived = new byte[1024];

                    int byteRecv = sender.Receive(messageReceived);
                    this.logger.Information("Message from Server -> {0}", Encoding.ASCII.GetString(messageReceived, 0, byteRecv));

                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
                }
                catch (ArgumentNullException ane)
                {
                    this.logger.Error("ArgumentNullException : {0}", ane.ToString());
                }
                catch (SocketException se)
                {
                    this.logger.Error("SocketException : {0}", se.ToString());
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