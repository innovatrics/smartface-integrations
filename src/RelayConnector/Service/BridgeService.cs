using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.RelayConnector.Models;
using Innovatrics.SmartFace.Integrations.RelayConnector.Factories;

namespace Innovatrics.SmartFace.Integrations.RelayConnector.Services
{
    public class BridgeService : IBridgeService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IRelayConnectorFactory relayConnectorFactory;

        public BridgeService(
            ILogger logger,
            IConfiguration configuration,
            IRelayConnectorFactory relayConnectorFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.relayConnectorFactory = relayConnectorFactory ?? throw new ArgumentNullException(nameof(relayConnectorFactory));
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var cameraToRelayMapping = this.getCameraMapping(notification.StreamId);

            if (cameraToRelayMapping == null)
            {
                this.logger.Warning("Stream {streamId} has not any mapping to Relay", notification.StreamId);
                return;
            }

            if (cameraToRelayMapping.WatchlistExternalIds != null)
            {
                if (cameraToRelayMapping.WatchlistExternalIds.Length > 0 && !cameraToRelayMapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
                {
                    this.logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
                    return;
                }
            }

            var relayConnector = this.relayConnectorFactory.Create(cameraToRelayMapping.Type);

            await relayConnector.OpenAsync(
                ipAddress: cameraToRelayMapping.IPAddress,
                port: cameraToRelayMapping.Port,
                channel: cameraToRelayMapping.Channel,
                username: cameraToRelayMapping.Username,
                password: cameraToRelayMapping.Password
            );
        }

        private RelayMapping getCameraMapping(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var relayMappings = this.configuration.GetSection("RelayMappings").Get<RelayMapping[]>();

            if (relayMappings == null)
            {
                return null;
            }

            return relayMappings
                        .Where(w => w.StreamId == streamGuid)
                        .FirstOrDefault();
        }
    }
}
