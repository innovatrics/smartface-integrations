using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceReference;


namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public interface ISmtpMailAdapter
    {

        Task<IList<KeilaLockers>> GetLockers();
    }
}