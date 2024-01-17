using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors
{
    public interface IAccessControlConnector
    {
        Task OpenAsync(string host, int? port, int? channel = null, string accessControlUserId = null,string username = null, string password = null);
        
        Task SendKeepAliveAsync(string host, int? port, int? channel = null, string accessControlUserId = null,string username = null, string password = null);
    }
}   