using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public interface IDataOrchestrator
    {
        Task GetLockersData();
        Task<LockerAnalytics> GetLockerAnalytics();
        Task<IList<AeosMember>> GetEmployees(); 
        Task<IList<AeosIdentifierType>> GetIdentifierTypes();
        Task<IList<AeosIdentifier>> GetIdentifiersPerType(long identifierType);
        Task<IList<AeosMember>> GetEmployeesByIdentifier(string identifier);
        
    }
}