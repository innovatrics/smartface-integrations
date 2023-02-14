using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AEOSConnector.Models;
using Innovatrics.SmartFace.Integrations.AEOSConnector.Factories;

namespace Innovatrics.SmartFace.Integrations.AEOSConnector.Services
{
    public class BridgeService : IBridgeService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IAEOSConnectorFactory AEOSConnectorFactory;

        public BridgeService(
            ILogger logger,
            IConfiguration configuration,
            IAEOSConnectorFactory AEOSConnectorFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.AEOSConnectorFactory = AEOSConnectorFactory ?? throw new ArgumentNullException(nameof(AEOSConnectorFactory));
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var cameraToAEOSMapping = this.getCameraMapping(notification.StreamId);

            if (cameraToAEOSMapping == null)
            {
                this.logger.Warning("Stream {streamId} has not any mapping to AEOS", notification.StreamId);
                return;
            }

            if (cameraToAEOSMapping.WatchlistExternalIds != null)
            {
                if (cameraToAEOSMapping.WatchlistExternalIds.Length > 0 && !cameraToAEOSMapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
                {
                    this.logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
                    return;
                }
            }

            var AEOSConnector = this.AEOSConnectorFactory.Create(cameraToAEOSMapping.Type);

            await AEOSConnector.OpenAsync(
                AEpuHostname: cameraToAEOSMapping.AEpuHostname,
                AEpuPort: cameraToAEOSMapping.AEpuPort,
                WatchlistMemberID: notification.WatchlistMemberId                
            );
        }

        private AEOSMapping getCameraMapping(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var AEOSMapping = this.configuration.GetSection("AEOSMapping").Get<AEOSMapping[]>();

            if (AEOSMapping == null)
            {
                return null;
            }

            return AEOSMapping
                        .Where(w => w.StreamId == streamGuid)
                        .FirstOrDefault();
        }

        private AEOSMapping[] getAllCameraMappings()
        {
            return this.configuration
                            .GetSection("AEOSMapping")
                            .Get<AEOSMapping[]>();
        }
    }
}
