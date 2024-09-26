using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.FingeraAdapter
{
    public interface IBridge
    {
        Task ProcessFaceGrantedNotificationAsync(FaceGrantedNotification notification);
    }
}