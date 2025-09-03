using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceReference;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public interface IDashboardsDataAdapter
    {
        Task<EmailSummaryResponse> GetEmailSummaryAssignmentChanges();
    }
}