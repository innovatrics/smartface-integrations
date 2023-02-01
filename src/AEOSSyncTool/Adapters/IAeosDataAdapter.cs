using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceReference;


namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface IAeosDataAdapter
    {
        Task<IList <AeosMember>>  getEmployees();
        Task<bool> createEmployees(AeosMember member, long badgeIdentifierType, long FreefieldDefinitionId);
        Task<bool> updateEmployee(AeosMember member, long FreefieldDefinitionId);
        Task<bool> removeEmployee(AeosMember member, long FreefieldDefinitionId);
        Task<findEmployeeResponse> getEmployeeId(string localSmartFaceId, long localFreefieldDefId);
        Task<long> getBadgeIdentifierType();
        Task<long> getFreefieldDefinitionId();
    }
}