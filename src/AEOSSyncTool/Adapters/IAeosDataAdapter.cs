using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface IAeosDataAdapter
    {
        Task<IList <AeosMember>>  getEmployees();
        Task<bool> createEmployees(AeosMember member, long badgeIdentifierType, long FreefieldDefinitionId);
        Task updateEmployees();
        Task removeEmployees();
        Task<long> getBadgeIdentifierType();
        Task<long> getFreefieldDefinitionId();
    }
}