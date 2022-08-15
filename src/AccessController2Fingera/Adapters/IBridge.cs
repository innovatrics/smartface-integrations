using System.Threading.Tasks;
using SmartFace.Integrations.Fingera.Notifications.DTO;

namespace SmartFace.Integrations.Fingera
{
    public interface IBridge
    {
        Task ProcessGrantedNotificationAsync(GrantedNotification notification);
    }
}