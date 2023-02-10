using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface ISmartFaceDataAdapter
    {
        Task<IList <SmartFaceMember>> getEmployees();
        Task<bool> createEmployee(SmartFaceMember member, string AeosWatchlistId);
        Task<bool> updateEmployee(SmartFaceMember member);
        Task<bool> removeEmployee(SmartFaceMember member);
        Task<string> initializeWatchlist();

    }
}