using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public interface ISmartFaceDataAdapter
    {
        Task<IList <SmartFaceMember>> GetEmployees();
        Task<bool> CreateEmployee(SmartFaceMember member, string aeosWatchlistId, string autoBiometryPrefix = null );
        Task<bool> UpdateEmployee(SmartFaceMember member, bool keepPhotoUpToDate);
        Task RemoveEmployee(SmartFaceMember member);
        Task<string> InitializeWatchlist();
        Task<byte[]> GetImageData(string imageDataId);

    }
}