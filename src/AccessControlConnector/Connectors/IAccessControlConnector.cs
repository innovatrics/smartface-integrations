using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors
{
    public interface IAccessControlConnector
    {
        Task OpenAsync(StreamConfig streamConfig, string accessControlUserId = null);
        
        Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null,string username = null, string password = null);
    }
}   