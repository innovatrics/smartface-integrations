using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers
{
    public interface  IUserResolver
    {
        Task<string> ResolveUserAsync(string watchlistMemberId);
    }
}