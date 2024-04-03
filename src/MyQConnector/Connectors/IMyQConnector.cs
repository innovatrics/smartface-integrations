using System;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace Innovatrics.SmartFace.Integrations.MyQConnector.Connectors
{
    public interface IMyQConnector
    {
        Task OpenAsync(string aepuHostname, int aepuPort, byte[] clientId);
    }
}