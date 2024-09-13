
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public interface IDebouncingService
    {
        bool IsBlocked(Notification notification, StreamMapping mapping);

        void Block(Notification notification, StreamMapping mapping);
    }
}