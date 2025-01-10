using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AEpuConnector.Services
{
    public interface IBridgeService
    {
        Task ProcessFaceGrantedNotificationAsync(GrantedNotification notification);
    }
}