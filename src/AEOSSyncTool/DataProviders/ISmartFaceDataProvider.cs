using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface ISmartFaceDataProvider
    {
        
        Task GetWatchlistMembers(string URL);

        bool SetWatchlistMembers();

    }
}