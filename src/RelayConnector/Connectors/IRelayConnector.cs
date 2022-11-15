using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.RelayConnector.Connectors
{
    public interface IRelayConnector
    {
        Task OpenAsync(string ipAddress, int port, int channel, string username = null, string password = null);
        Task SendKeepAliveAsync(string ipAddress, int port, int? channel = null, string username = null, string password = null);
    }
}