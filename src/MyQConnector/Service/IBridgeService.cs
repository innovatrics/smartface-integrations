using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Services
{
    public interface IBridgeService
    {
        Task ProcessGrantedNotificationAsync(GrantedNotification notification);
    }
}