using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class ValidationService : IValidationService
    {
        public bool ValidateNotification(Notification22 notification)
        {
            return true;
        }
    }
}