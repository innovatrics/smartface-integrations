using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Connectors
{
    public class MyQConnector : IMyQConnector
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private Socket socket;
        private string PrinterConnection;

        public MyQConnector(
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

        private async Task<Socket> CreateOpenSocketAsync(string myqHostname, int myqPort)
        {
            try
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(myqHostname, myqPort);

                this.logger.Information($"Socket connected to {myqHostname}:{myqPort}");

                return socket;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"Socket failed to connect to {myqHostname}:{myqPort}");
                throw ex;
            }
        }

        public async Task OpenAsync(string myqHostname, int myqPort)
        {
            this.logger.Information("Sending ipBadge to {myqHostname}:{myqPort}", myqHostname, myqPort);

            try
            {
                if (socket == null)
                {
                    socket = await CreateOpenSocketAsync(myqHostname, myqPort);
                }

                // DO REST API CALLS

                /*

                - check if the printer is already unlocked
                - if it is not unlocked proceed
                - find out email address of the user, find users account code
                - get authentication token
                - unlock printer

                */

                


                
            }

            catch (Exception e)
            {
                this.logger.Error(e.ToString());
            }

        }

    }
}