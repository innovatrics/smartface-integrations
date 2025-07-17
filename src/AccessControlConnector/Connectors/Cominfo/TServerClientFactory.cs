using System.Net;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo
{
    public class TServerClientFactory : ITServerClientFactory
    {
        public ITServerClient Create(IPAddress ipAddress, int port)
        {
            return new TServerClient(ipAddress, port);
        }
    }
} 