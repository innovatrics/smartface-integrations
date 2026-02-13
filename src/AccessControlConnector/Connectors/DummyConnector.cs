using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;
using System.Diagnostics;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Telemetry;

namespace AccessControlConnector.Connectors
{
    internal class DummyConnector : IAccessControlConnector
    {
        /// <summary>
        /// Only for easier debug and integration purpose.
        /// </summary>
        internal const bool Enabled = false;

        public async Task OpenAsync(StreamConfig streamConfig, string accessControlUserId = null)
        {
            using var activity = AccessControlTelemetry.ActivitySource.StartActivity(
                AccessControlTelemetry.ExternalCallSpanName,
                ActivityKind.Client);

            activity?.SetTag(AccessControlTelemetry.ConnectorNameAttribute, "Dummy");

            await Task.Delay(100);
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null,
            string username = null, string password = null)
        {
            return Task.CompletedTask;
        }
    }
}
