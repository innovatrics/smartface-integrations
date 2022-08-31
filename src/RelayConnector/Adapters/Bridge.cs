using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.RelayConnector.Models;

namespace Innovatrics.SmartFace.Integrations.RelayConnector
{
    public class Bridge : IBridge
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IRelayConnector relayConnector;

        public Bridge(
            ILogger logger,
            IConfiguration configuration,
            IRelayConnector relayConnector
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.relayConnector = relayConnector ?? throw new ArgumentNullException(nameof(relayConnector));
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

            await this.relayConnector.OpenAsync(
                ipAddress: cameraToRelayMapping.IPAddress,
                port: cameraToRelayMapping.Port,
                channel: cameraToRelayMapping.Channel,
                authUsername: cameraToRelayMapping.AuthUsername,
                authPassword: cameraToRelayMapping.AuthPassword
            );
        }

        private CameraMappingConfig getCameraMapping(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var cameraMappings = this.configuration.GetSection("Relay:Cameras").Get<CameraMappingConfig[]>();

            if (cameraMappings == null)
            {
                return null;
            }

            return cameraMappings
                        .Where(w => w.StreamId == streamGuid)
                        .FirstOrDefault();
        }
    }
}
