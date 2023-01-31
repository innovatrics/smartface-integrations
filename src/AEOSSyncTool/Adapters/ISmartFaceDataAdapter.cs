using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface ISmartFaceDataAdapter
    {
        //Task OpenAsync(string checkpoint_id, string ticket_id, int chip_id = 12);
        Task<IList <SmartFaceMember>> getEmployees();
        Task createEmployees();
        Task updateEmployees();
        Task removeEmployees();
    }
}