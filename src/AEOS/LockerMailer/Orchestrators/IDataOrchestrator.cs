using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public interface IDataOrchestrator
    {
        Task<IList<AeosIdentifierType>> GetIdentifierTypes();
        Task<IList<AeosIdentifier>> GetIdentifiersPerType(long identifierType);
        
    }
}