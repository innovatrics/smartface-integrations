using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AEpuConnector.Connectors
{
    public interface IAEpuConnector
    {
        Task OpenAsync(string aepuHostname, int aepuPort, string watchlistMemberID);
    }
}