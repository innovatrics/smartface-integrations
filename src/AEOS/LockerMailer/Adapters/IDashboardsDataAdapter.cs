using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceReference;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public interface IDashboardsDataAdapter
    {
        Task<EmailSummaryResponse> GetEmailSummaryAssignmentChanges();
        Task<List<GroupInfo>> GetGroups();
        Task<LockerReleaseResult> ReleaseLockerAsync(int lockerId);
        Task<List<LockerAccessEvent>> GetAccessedLockersAsync(DateTime? fromDateTime = null);
    }

    public class LockerReleaseResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public int LockerId { get; set; }
    }
}