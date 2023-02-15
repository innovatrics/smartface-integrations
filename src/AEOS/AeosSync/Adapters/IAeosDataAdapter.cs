using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceReference;


namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public interface IAeosDataAdapter
    {
        Task<IList<AeosMember>> GetEmployees();
        Task<bool> CreateEmployees(AeosMember member, long badgeIdentifierType, long freefieldDefinitionId);
        Task<bool> UpdateEmployee(AeosMember member, long freefieldDefinitionId);
        Task<bool> RemoveEmployee(AeosMember member, long freefieldDefinitionId);
        Task<findEmployeeResponse> GetEmployeeId(string localSmartFaceId, long localFreefieldDefId);
        Task<long> GetBadgeIdentifierType();
        Task<long> GetFreefieldDefinitionId();
    }
}