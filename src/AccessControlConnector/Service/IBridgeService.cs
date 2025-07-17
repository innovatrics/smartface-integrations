using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public interface IBridgeService
    {
        Task ProcessGrantedNotificationAsync(AccessControlMapping mapping, GrantedNotification notification);

        Task ProcessDeniedNotificationAsync(AccessControlMapping mapping, DeniedNotification notification);

        Task ProcessBlockedNotificationAsync(AccessControlMapping mapping, BlockedNotification notification);

        Task SendKeepAliveSignalAsync();
    }
}