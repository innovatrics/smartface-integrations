using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface IBridge
    {
        Task ConnectToAEOS();
    }
}