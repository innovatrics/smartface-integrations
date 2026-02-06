using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.FingeraAdapter.Resolvers
{
    public interface IUserResolver
    {
        Task<string> ResolveUserAsync(GrantedNotification notification);
    }
}
