
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public interface IValidationService
    {
        bool ValidateNotification(Notification22 notification);
    }
}