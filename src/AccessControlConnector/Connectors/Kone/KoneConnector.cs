using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Kone.Api.Client;
using Serilog;

namespace AccessControlConnector.Connectors.Kone
{
    public class KoneConnector : IAccessControlConnector
    {
        private readonly KoneApiGateWay _koneApiGateWay;
        private readonly ILogger _log;
        private readonly ActionBlock<(Guid LandingCallId, Task<bool> LandingCallTask)> _ab;

        public KoneConnector(KoneApiGateWay koneApiGateWay, ILogger log)
        {
            _koneApiGateWay = koneApiGateWay ?? throw new ArgumentNullException(nameof(koneApiGateWay));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            _ab = new ActionBlock<(Guid LandingCallId, Task<bool> LandingCallTask)>(async t =>
            {
                try
                {
                    var accepted = await t.LandingCallTask;
                    _log.Information("Landing call finished with accepted state {Accepted}", accepted);
                }
                catch (Exception e)
                {
                    _log.Error(e, "Landing call with Id {LandingCallId} failed", t.LandingCallId);
                }

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 10
            });
        }

        public async Task OpenAsync(AccessConnectorConfig accessControlMapping, string accessControlUserId = null)
        {
            var maxStateUpdateWaitTime = TimeSpan.FromSeconds(3);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var areaId = accessControlMapping.DestinationArea;
            var isDirectionUp = accessControlMapping.IsDirectionUp;
            var landingCallId = Guid.NewGuid();

            _log.Information("Sending landing call with Id {Id} to area {areaId}, direction up: {isDirectionUp}",
                landingCallId, areaId, isDirectionUp);

            var landingCallTask = _koneApiGateWay.SendLandingCallToAreaIfNotInProgressAsync(areaId, isDirectionUp, maxStateUpdateWaitTime,
                cts.Token);

            await _ab.SendAsync((landingCallId, landingCallTask), CancellationToken.None);
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null,
            string username = null, string password = null)
        {
            return Task.CompletedTask;
        }
    }
}
