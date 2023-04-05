using System;
using System.Linq;
using System.Text;
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
                this.logger.Information("Stream {streamId} has not any mapping to AEpu", notification.StreamId);
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

            if (!this.tryParseClientId(notification.WatchlistExternalId, out var encodedClientId))
            {
                this.logger.Information("Watchlist {watchlistExternalId} did not pass validation criteria", notification.WatchlistExternalId);
                return;
            }

            var AEpuConnector = this.AEpuConnectorFactory.Create(cameraToAEpuMapping.Type);

            this.logger.Information("Open {AEpuHostname} for user {WatchlistMemberFullName} ({WatchlistMemberID})", cameraToAEpuMapping.AEpuHostname, notification.WatchlistMemberFullName, notification.WatchlistExternalId);

            await AEpuConnector.OpenAsync(
                aepuHostname: cameraToAEpuMapping.AEpuHostname,
                aepuPort: cameraToAEpuMapping.AEpuPort,
                clientId: encodedClientId
            );
        }

        private bool tryParseClientId(
            string watchlistExternalId,
            out byte[] clientId
        )
        {
            clientId = null;
            var watchlistIdAsBytes = Encoding.UTF8.GetBytes(watchlistExternalId);

            if (watchlistIdAsBytes.Length > 28 || watchlistIdAsBytes.Length < 1)
            {
                this.logger.Debug($"{nameof(watchlistExternalId)} converted to byte[] must be in range of 1 to 28 bytes, current length: {watchlistIdAsBytes.Length}");
                return false;
            }

            clientId = watchlistIdAsBytes;
            return true;
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
