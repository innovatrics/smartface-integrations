using System.Threading.Tasks;
using System.Collections.Generic;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public interface ISmtpMailAdapter
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
        Task SendAsync(string toEmail, string subject, string htmlBody, MailLoggingData? loggingData);
    }

    public class MailLoggingData
    {
        public string TemplateUsed { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public int? EmployeeId { get; set; }
        public Dictionary<string, string?> VariableDump { get; set; } = new Dictionary<string, string?>();
    }
}