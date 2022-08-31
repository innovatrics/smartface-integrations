using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.RelayConnector
{
    public interface IBridge
    {
        Task ProcessGrantedNotificationAsync(GrantedNotification notification);
    }
}