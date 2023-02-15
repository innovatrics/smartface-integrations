using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AEpuConnector.Models;
using Innovatrics.SmartFace.Integrations.AEpuConnector.Factories;

namespace Innovatrics.SmartFace.Integrations.AEpuConnector.Services
{
    public class BridgeService : IBridgeService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IAEpuConnectorFactory AEpuConnectorFactory;

        public BridgeService(
            ILogger logger,
            IConfiguration configuration,
            IAEpuConnectorFactory AEpuConnectorFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.AEpuConnectorFactory = AEpuConnectorFactory ?? throw new ArgumentNullException(nameof(AEpuConnectorFactory));
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var cameraToAEpuMapping = this.getCameraMapping(notification.StreamId);

            if (cameraToAEpuMapping == null)
            {
                this.logger.Warning("Stream {streamId} has not any mapping to AEpu", notification.StreamId);
                return;
            }

            if (cameraToAEpuMapping.WatchlistExternalIds != null)
            {
                if (cameraToAEpuMapping.WatchlistExternalIds.Length > 0 && !cameraToAEpuMapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
                {
                    this.logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
                    return;
                }
            }

            var AEpuConnector = this.AEpuConnectorFactory.Create(cameraToAEpuMapping.Type);

            await AEpuConnector.OpenAsync(
                AEpuHostname: cameraToAEpuMapping.AEpuHostname,
                AEpuPort: cameraToAEpuMapping.AEpuPort,
                WatchlistMemberID: notification.WatchlistMemberId                
            );
        }

        private AEpuMapping getCameraMapping(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var AEpuMapping = this.configuration.GetSection("AEpuMapping").Get<AEpuMapping[]>();

            if (AEpuMapping == null)
            {
                return null;
            }

            return AEpuMapping
                        .Where(w => w.StreamId == streamGuid)
                        .FirstOrDefault();
        }

        private AEpuMapping[] getAllCameraMappings()
        {
            return this.configuration
                            .GetSection("AEpuMapping")
                            .Get<AEpuMapping[]>();
        }
    }
}
