using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers
{
    public interface  IUserResolver
    {
        Task<string> ResolveUserAsync(IIdentifiedNotification notification);
    }
}