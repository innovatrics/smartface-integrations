using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AEpuConnector.Connectors
{
    public interface IAEpuConnector
    {
        Task OpenAsync(string AEpuHostname, int AEpuPort, string WatchlistMemberID);
    }
}