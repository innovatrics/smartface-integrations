using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public interface ISmartFaceDataAdapter
    {
        Task<IList <SmartFaceMember>> GetEmployees();
        Task<bool> CreateEmployee(SmartFaceMember member, string aeosWatchlistId);
        Task<bool> UpdateEmployee(SmartFaceMember member);
        Task<bool> RemoveEmployee(SmartFaceMember member);
        Task<string> InitializeWatchlist();

    }
}