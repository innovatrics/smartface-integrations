using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface IAEOSSyncAdapter
    {
        Task OpenAsync(string checkpoint_id, string ticket_id, int chip_id = 12);
    }
}