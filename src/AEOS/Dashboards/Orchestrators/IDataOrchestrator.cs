using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public interface IDataOrchestrator
    {
        Task GetLockersData();
        Task<LockerAnalytics> GetLockerAnalytics();
    }
}