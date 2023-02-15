using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public interface ISmartFaceDataAdapter
    {
        Task<IList <SmartFaceMember>> getEmployees();
        Task<bool> createEmployee(SmartFaceMember member, string aeosWatchlistId);
        Task<bool> updateEmployee(SmartFaceMember member);
        Task<bool> removeEmployee(SmartFaceMember member);
        Task<string> initializeWatchlist();

    }
}