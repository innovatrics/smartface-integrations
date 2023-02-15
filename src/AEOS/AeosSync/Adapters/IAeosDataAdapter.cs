using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceReference;


namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public interface IAeosDataAdapter
    {
        Task<IList<AeosMember>> getEmployees();
        Task<bool> createEmployees(AeosMember member, long badgeIdentifierType, long freefieldDefinitionId);
        Task<bool> updateEmployee(AeosMember member, long freefieldDefinitionId);
        Task<bool> removeEmployee(AeosMember member, long freefieldDefinitionId);
        Task<findEmployeeResponse> getEmployeeId(string localSmartFaceId, long localFreefieldDefId);
        Task<long> getBadgeIdentifierType();
        Task<long> getFreefieldDefinitionId();
    }
}