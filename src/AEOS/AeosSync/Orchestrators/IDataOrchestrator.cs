using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public interface IDataOrchestrator
    {
        Task Synchronize();
    }
}