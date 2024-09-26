using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.FaceGate
{
    public interface IBridge
    {
        Task ProcessFaceGrantedNotificationAsync(FaceGrantedNotification notification);
    }
}