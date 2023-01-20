using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AEOSConnector.Connectors
{
    public class AEpuConnector : IAEOSConnector
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
                // Establish the remote endpoint for the socket.

                if (System.Net.Dns.GetHostAddresses(AEpuHostname).Length == 0)
                {
                    throw new ArgumentException(
                        "Unable to retrieve address from specified host name.", 
                        "hostName"
                    );
                }
                else
                {
                    System.Net.IPHostEntry ipHost = System.Net.Dns.GetHostEntry(AEpuHostname);
                    ipAddr = ipHost.AddressList[0];
                    localEndPoint = new System.Net.IPEndPoint(ipAddr, AEpuPort);

                    this.logger.Information("IP Address Received for the hostname {AEpuHostname}: {ipAddr}",AEpuHostname,ipAddr);
                }

                // Creation TCP/IP Socket using
                // Socket Class Constructor
                Socket sender = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                
                try 
                {
             
                    // Connect Socket to the remote
                    // endpoint using method Connect()
                    sender.Connect(localEndPoint);


                    // We print EndPoint information
                    // that we are connected
                    this.logger.Information("Socket connected to -> {0} ", sender.RemoteEndPoint.ToString());

                    var clientId = WatchlistMemberID;
                    var encodedClientId = Encoding.UTF8.GetBytes(clientId);
                    
                    if (encodedClientId.Length > 28)
                        throw new ApplicationException("Client ID is too long");

                    // Message To Be Sent
                    // Byte nr.            0               1                   2                   n                   n+1                 n+2
                    // Content             STX             length              Badge  byte 1       Badge byte n        Checksum            ETX
                    
                    var messageBytes = new byte[2 + encodedClientId.Length + 2];
                    messageBytes[0] = 0x02; // STX
                    messageBytes[1] = (byte)encodedClientId.Length;
                    for(int i= 0;i < encodedClientId.Length ;i++)
                    {
                        messageBytes[2+i] = encodedClientId[i];
                    }

                    byte checksum = 0;

                    for(int i= 0;i < encodedClientId.Length +1;i++)
                    {
                        checksum = (byte)(checksum ^ messageBytes[1+i]);
                    }

                    messageBytes[messageBytes.Length - 2] = checksum;
                    messageBytes[messageBytes.Length - 1] = 0x03; // ETX

                    //int byteSent = sender.Send(messageSent);
                    //Console.WriteLine(System.Convert.ToHexString(messageBytes));
                    int byteSent = sender.Send(messageBytes);
        
                    // Data buffer
                    byte[] messageReceived = new byte[1024];
        
                    // We receive the message using
                    // the method Receive(). This
                    // method returns number of bytes
                    // received, that we'll use to
                    // convert them to string
                    int byteRecv = sender.Receive(messageReceived);
                    this.logger.Information("Message from Server -> {0}",Encoding.ASCII.GetString(messageReceived,0, byteRecv));

                    // Close Socket using
                    // the method Close()
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
                }
                // Manage of Socket's Exceptions
                catch (ArgumentNullException ane) 
                {
                    this.logger.Information("ArgumentNullException : {0}", ane.ToString());
                }
                
                catch (SocketException se) 
                {   
                    this.logger.Information("SocketException : {0}", se.ToString());
                }
                
                catch (Exception e) {
                    this.logger.Information("Unexpected exception : {0}", e.ToString());
                }

            }

            catch (Exception e) 
            {
                this.logger.Information(e.ToString());
            }
        
            /*
            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"http://{ipAddress}:{port}/do_value/slot_0/";

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var payload = new
            {
                DOVal = new[] {
                    new {
                        Ch = channel,
                        Val = 1
                    },
                    new {
                        Ch = channel,
                        Val = 0
                    }
                }
            };

            httpRequest.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            var result = await httpClient.SendAsync(httpRequest);
            string resultContent = await result.Content.ReadAsStringAsync();

            if (result.IsSuccessStatusCode)
            {
                this.logger.Information("OK");
            }
            else
            {
                this.logger.Error("Fail with {statusCode}", result.StatusCode);
            }
            */
        }

        public async Task SendKeepAliveAsync(string AEpuHostname, int AEpuPort)
        {
            // is this needed in AEOS? Connector
            /*
            this.logger.Information("Send KeepAlive to {ipAddress}:{port}/di_value/slot_0/ and channel: {channel}", ipAddress, port, channel);

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"http://{ipAddress}:{port}/di_value/slot_0/";

            if (channel != null)
            {
                requestUri += $"{channel}";
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            }

            var result = await httpClient.SendAsync(httpRequest);
            string resultContent = await result.Content.ReadAsStringAsync();

            if (result.IsSuccessStatusCode)
            {
                this.logger.Information("OK");
            }
            else
            {
                this.logger.Error("Fail with {statusCode}", result.StatusCode);
            }
            */
        }
    }
}