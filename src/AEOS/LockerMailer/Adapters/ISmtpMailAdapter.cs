using System.Threading.Tasks;
using System.Collections.Generic;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public interface ISmtpMailAdapter
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
    }
}