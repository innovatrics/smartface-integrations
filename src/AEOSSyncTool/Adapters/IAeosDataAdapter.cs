using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface IAeosDataAdapter
    {
        Task<IList <AeosMember>>  getEmployees();
        Task<bool> createEmployees(string AEOSendpoint, string AEOSusername, string AEOSpassword, List<AeosMember> member);
        Task updateEmployees();
        Task removeEmployees();
    }
}