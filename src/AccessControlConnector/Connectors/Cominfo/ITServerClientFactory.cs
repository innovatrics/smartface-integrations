using System.Net;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo
{
    public interface ITServerClientFactory
    {
        ITServerClient Create(IPAddress ipAddress, int port);
    }
} 