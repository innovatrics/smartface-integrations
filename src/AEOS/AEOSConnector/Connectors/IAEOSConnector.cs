using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AEOSConnector.Connectors
{
    public interface IAEOSConnector
    {
        Task OpenAsync(string AEpuHostname, int AEpuPort, string WatchlistMemberID);
        Task SendKeepAliveAsync(string AEpuHostname, int AEpuPort);
    }
}