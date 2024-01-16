using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public class BridgeService : IBridgeService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IAccessControlConnectorFactory relayConnectorFactory;

        public BridgeService(
            ILogger logger,
            IConfiguration configuration,
            IAccessControlConnectorFactory relayConnectorFactory
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

            var cameraToAccessControlMapping = this.getCameraMapping(notification.StreamId);

            if (cameraToAccessControlMapping == null)
            {
                this.logger.Warning("Stream {streamId} has not any mapping to AccessControl", notification.StreamId);
                return;
            }

            if (cameraToAccessControlMapping.WatchlistExternalIds != null)
            {
                if (cameraToAccessControlMapping.WatchlistExternalIds.Length > 0 && !cameraToAccessControlMapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
                {
                    this.logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
                    return;
                }
            }

            var relayConnector = this.relayConnectorFactory.Create(cameraToAccessControlMapping.Type);

            await relayConnector.OpenAsync(
                ipAddress: cameraToAccessControlMapping.IPAddress,
                port: cameraToAccessControlMapping.Port,
                channel: cameraToAccessControlMapping.Channel,
                username: cameraToAccessControlMapping.Username,
                password: cameraToAccessControlMapping.Password
            );
        }

        public async Task SendKeepAliveSignalAsync()
        {
            var cameraToAccessControlMapping = this.getAllCameraMappings();

            if (cameraToAccessControlMapping == null)
            {
                this.logger.Warning("No mapping to AccessControl configured");
                return;
            }

            var uniqueAccessControls = cameraToAccessControlMapping
                                                .GroupBy(g => new
                                                {
                                                    g.Type,
                                                    g.IPAddress,
                                                    g.Port,
                                                    g.Username,
                                                    g.Password
                                                })
                                                .ToArray();

            foreach (var uniqueMapping in uniqueAccessControls)
            {
                var relayConnector = this.relayConnectorFactory.Create(uniqueMapping.Key.Type);

                await relayConnector.SendKeepAliveAsync(
                    ipAddress: uniqueMapping.Key.IPAddress,
                    port: uniqueMapping.Key.Port,
                    channel: uniqueMapping.First().Channel,
                    username: uniqueMapping.Key.Username,
                    password: uniqueMapping.Key.Password
                );
            }
        }

        private AccessControlMapping getCameraMapping(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var accessControlMapping = this.configuration.GetSection("AccessControlMapping").Get<AccessControlMapping[]>();

            if (accessControlMapping == null)
            {
                return null;
            }

            return accessControlMapping
                        .Where(w => w.StreamId == streamGuid)
                        .FirstOrDefault();
        }

        private AccessControlMapping[] getAllCameraMappings()
        {
            return this.configuration
                            .GetSection("AccessControlMapping")
                            .Get<AccessControlMapping[]>();
        }
    }
}
