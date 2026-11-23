using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Telemetry;
using Kone.Api.Client;
using OpenTelemetry.Trace;
using Serilog;

namespace AccessControlConnector.Connectors.Kone
{
    public class KoneConnector : IAccessControlConnector
    {
        private readonly KoneApiGateWay _koneApiGateWay;
        private readonly ILogger _log;
        private readonly ActionBlock<(Guid LandingCallId, int DestinationArea, Task<bool> LandingCallTask)> _ab;
        private readonly TimeSpan _landingCallTimeout;
        private readonly TimeSpan _maxStateUpdateWaitTime;

        /// <param name="landingCallTimeout">How long to wait for a landing call to be served (default 15 s).</param>
        /// <param name="maxStateUpdateWaitTime">How long to wait for the first call state from KONE (default 5 s); must be
        /// shorter than landingCallTimeout.</param>
        public KoneConnector(KoneApiGateWay koneApiGateWay, ILogger log,
            TimeSpan? landingCallTimeout = null, TimeSpan? maxStateUpdateWaitTime = null)
        {
            _koneApiGateWay = koneApiGateWay ?? throw new ArgumentNullException(nameof(koneApiGateWay));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _landingCallTimeout = landingCallTimeout ?? TimeSpan.FromSeconds(15);
            _maxStateUpdateWaitTime = maxStateUpdateWaitTime ?? TimeSpan.FromSeconds(5);

            if (_landingCallTimeout <= TimeSpan.Zero || _maxStateUpdateWaitTime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(_landingCallTimeout <= TimeSpan.Zero ? nameof(landingCallTimeout) : nameof(maxStateUpdateWaitTime),
                    "landingCallTimeout and maxStateUpdateWaitTime must be positive");
            }

            // Otherwise the landing-call timeout could end a call that got no call state at all, and it would be
            // reported below as accepted by KONE
            if (_maxStateUpdateWaitTime >= _landingCallTimeout)
            {
                throw new ArgumentException(
                    $"maxStateUpdateWaitTime ({_maxStateUpdateWaitTime}) must be shorter than landingCallTimeout ({_landingCallTimeout})");
            }

            _ab = new ActionBlock<(Guid LandingCallId, int DestinationArea, Task<bool> LandingCallTask)>(async t =>
            {
                try
                {
                    var sent = await t.LandingCallTask;

                    if (sent)
                    {
                        _log.Information("Landing call with Id {LandingCallId} to area {DestinationArea} finished",
                            t.LandingCallId, t.DestinationArea);
                    }
                    else
                    {
                        _log.Information("Landing call with Id {LandingCallId} to area {DestinationArea} skipped: a call to this area is already in progress",
                            t.LandingCallId, t.DestinationArea);
                    }
                }
                catch (OperationCanceledException)
                {
                    // KONE accepted the call and sent call states, but did not report it served before the wait ended:
                    // usually a lift that needs longer, possibly a monitor connection that dropped. Not a failed call.
                    _log.Information("Landing call with Id {LandingCallId} to area {DestinationArea} was accepted by KONE (call states received) but not reported served within {LandingCallTimeout}; stopped waiting",
                        t.LandingCallId, t.DestinationArea, _landingCallTimeout);
                }
                catch (Exception e)
                {
                    _log.Error(e, "Landing call with Id {LandingCallId} to area {DestinationArea} failed",
                        t.LandingCallId, t.DestinationArea);
                }

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 10
            });
        }

        public async Task OpenAsync(StreamConfig streamConfig, string accessControlUserId = null)
        {
            var maxStateUpdateWaitTime = _maxStateUpdateWaitTime;
            var cts = new CancellationTokenSource(_landingCallTimeout);

            var areaId = streamConfig.DestinationArea;
            var isDirectionUp = streamConfig.IsDirectionUp;
            var landingCallId = Guid.NewGuid();

            _log.Information("Sending landing call with Id {LandingCallId} to area {DestinationArea}, direction up: {IsDirectionUp}",
                landingCallId, areaId, isDirectionUp);

            using var activity = AccessControlTelemetry.ActivitySource.StartActivity(
                AccessControlTelemetry.ExternalCallSpanName,
                ActivityKind.Client);

            activity?.SetTag("network.protocol", "websocket");
            activity?.SetTag(AccessControlTelemetry.ConnectorNameAttribute, "KONE");
            activity?.SetTag("kone.area_id", areaId);
            activity?.SetTag("kone.direction_up", isDirectionUp);

            try
            {
                var landingCallTask = _koneApiGateWay.SendLandingCallToAreaIfNotInProgressAsync(areaId, isDirectionUp, maxStateUpdateWaitTime,
                    cts.Token);

                await _ab.SendAsync((landingCallId, areaId, landingCallTask), CancellationToken.None);
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null,
            string username = null, string password = null)
        {
            return Task.CompletedTask;
        }
    }
}
