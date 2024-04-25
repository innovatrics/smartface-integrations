using System;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace Innovatrics.SmartFace.Integrations.MyQConnector.Connectors
{
    public interface IConnector
    {
        Task OpenAsync(string myqPrinter, Guid myqStringId, string watchlistMemberId);
    }
}