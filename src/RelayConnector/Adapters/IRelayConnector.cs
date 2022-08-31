using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.RelayConnector
{
    public interface IRelayConnector
    {
        Task OpenAsync(string ipAddress, int port, int channel, string authUsername = null, string authPassword = null);
    }
}