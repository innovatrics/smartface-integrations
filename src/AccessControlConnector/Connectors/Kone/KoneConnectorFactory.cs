using System;
using System.Threading;
using Kone.Api.Client;
using Kone.Api.Client.Clients;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AccessControlConnector.Connectors.Kone
{
    public class KoneConnectorFactory
    {
        private static readonly object Lock = new();
        private static KoneConnector _koneConnector;

        public static KoneConnector Create(ILogger log, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(log);
            ArgumentNullException.ThrowIfNull(configuration);

            lock (Lock)
            {
                if (_koneConnector != null)
                {
                    return _koneConnector;
                }

                var koneConfiguration = configuration.GetSection("KoneConfiguration").Get<KoneConfiguration>();

                log.Information("Kone configuration is {@KoneConfiguration}", koneConfiguration);

                var authClient = new KoneAuthApiClient(koneConfiguration.ClientId, koneConfiguration.ClientSecret);

                var diagnosticCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                KoneDiagnostics.LogInfoAsync(authClient, log, diagnosticCts.Token,
                    fullDiagnostic: koneConfiguration.LogFullStartupDiagnostics,
                    koneConfiguration.GroupId).GetAwaiter().GetResult();

                var buildingApiClient = new KoneBuildingApiClient(log.ForContext<KoneBuildingApiClient>(), authClient,
                    koneConfiguration.BuildingId,
                    koneConfiguration.GroupId,
                    koneConfiguration.WebSocketEndpoint);

                var gateway = new KoneApiGateWay(buildingApiClient, log.ForContext<KoneApiGateWay>());
                _koneConnector = new KoneConnector(gateway, log.ForContext<KoneConnector>());
                return _koneConnector;
            }
        }
    }
}
