using System;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Connectors
{
    public interface IMyQConnector
    {
        Task OpenAsync(string myqPrinter, Guid myqStringId, string watchlistMemberId);
    }
}