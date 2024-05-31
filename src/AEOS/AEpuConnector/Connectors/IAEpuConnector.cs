using System;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace Innovatrics.SmartFace.Integrations.AEpuConnector.Connectors
{
    public interface IAEpuConnector
    {
        Task OpenAsync(string aepuHostname, int aepuPort, byte[] clientId);
    }
}