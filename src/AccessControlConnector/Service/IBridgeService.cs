using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public interface IBridgeService
    {
        Task ProcessGrantedNotificationAsync(GrantedNotification notification);

        Task ProcessDeniedNotificationAsync(DeniedNotification notification);

        Task ProcessBlockedNotificationAsync(BlockedNotification notification);

        Task SendKeepAliveSignalAsync();
    }
}