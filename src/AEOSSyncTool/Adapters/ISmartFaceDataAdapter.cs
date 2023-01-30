using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface ISmartFaceDataAdapter
    {
        //Task OpenAsync(string checkpoint_id, string ticket_id, int chip_id = 12);
        Task OpenAsync();
        Task getEmployees();
        Task createEmployees();
        Task updateEmployees();
        Task removeEmployees();
    }
}