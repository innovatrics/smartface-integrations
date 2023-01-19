using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AOESConnector.Models;
using Innovatrics.SmartFace.Integrations.AOESConnector.Factories;

namespace Innovatrics.SmartFace.Integrations.AOESConnector.Services
{
    public class BridgeService : IBridgeService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IAOESConnectorFactory AOESConnectorFactory;

        public BridgeService(
            ILogger logger,
            IConfiguration configuration,
            IAOESConnectorFactory AOESConnectorFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.AOESConnectorFactory = AOESConnectorFactory ?? throw new ArgumentNullException(nameof(AOESConnectorFactory));
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var cameraToAOESMapping = this.getCameraMapping(notification.StreamId);

            if (cameraToAOESMapping == null)
            {
                this.logger.Warning("Stream {streamId} has not any mapping to AOES", notification.StreamId);
                return;
            }

            if (cameraToAOESMapping.WatchlistExternalIds != null)
            {
                if (cameraToAOESMapping.WatchlistExternalIds.Length > 0 && !cameraToAOESMapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
                {
                    this.logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
                    return;
                }
            }

            var AOESConnector = this.AOESConnectorFactory.Create(cameraToAOESMapping.Type);

            await AOESConnector.OpenAsync(
                ipAddress: cameraToAOESMapping.IPAddress,
                port: cameraToAOESMapping.Port,
                channel: cameraToAOESMapping.Channel,
                username: cameraToAOESMapping.Username,
                password: cameraToAOESMapping.Password
            );
        }

        public async Task SendKeepAliveSignalAsync()
        {
            var cameraToAOESMappings = this.getAllCameraMappings();

            if (cameraToAOESMappings == null)
            {
                this.logger.Warning("No mapping to AOES configured");
                return;
            }

            var uniqueAOESs = cameraToAOESMappings.GroupBy(g => new
            {
                g.Type,
                g.IPAddress,
                g.Port,
                g.Username,
                g.Password
            }).ToArray();

            foreach (var cameraToAOESMapping in uniqueAOESs)
            {
                var AOESConnector = this.AOESConnectorFactory.Create(cameraToAOESMapping.Key.Type);

                await AOESConnector.SendKeepAliveAsync(
                    ipAddress: cameraToAOESMapping.Key.IPAddress,
                    port: cameraToAOESMapping.Key.Port,
                    channel: cameraToAOESMapping.First().Channel,
                    username: cameraToAOESMapping.Key.Username,
                    password: cameraToAOESMapping.Key.Password
                );
            }
        }

        private AOESMapping getCameraMapping(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var AOESMappings = this.configuration.GetSection("AOESMappings").Get<AOESMapping[]>();

            if (AOESMappings == null)
            {
                return null;
            }

            return AOESMappings
                        .Where(w => w.StreamId == streamGuid)
                        .FirstOrDefault();
        }

        private AOESMapping[] getAllCameraMappings()
        {
            return this.configuration
                            .GetSection("AOESMappings")
                            .Get<AOESMapping[]>();
        }
    }
}
