using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public interface IBridgeService
    {
        Task ProcessFaceGrantedNotificationAsync(FaceGrantedNotification notification);
        
        Task SendKeepAliveSignalAsync();
    }
}