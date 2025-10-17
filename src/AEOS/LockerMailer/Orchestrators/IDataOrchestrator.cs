using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using System.Collections.Generic;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public interface IDataOrchestrator
    {
        Task ProcessEmailSummaryAssignmentChanges(EmailSummaryResponse emailSummary);
    }
}