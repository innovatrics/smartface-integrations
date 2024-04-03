using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Serilog;

using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.MyQConnector.Models;
using Innovatrics.SmartFace.Integrations.MyQConnector.Factories;


namespace Innovatrics.SmartFace.Integrations.MyQConnector.Services
{
    public class BridgeService : IBridgeService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IMyQConnectorFactory myQConnectorFactory;

        public BridgeService(
            ILogger logger,
            IConfiguration configuration,
            IMyQConnectorFactory MyQConnectorFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.MyQConnectorFactory = MyQConnectorFactory ?? throw new ArgumentNullException(nameof(MyQConnectorFactory));
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

            if (!this.tryParseClientId(notification.WatchlistMemberExternalId, out var encodedClientId))
            {
                this.logger.Information("WatchlistMember {WatchlistMemberExternalId} did not pass validation criteria", notification.WatchlistMemberExternalId);
                return;
            }

            var AEpuConnector = this.AEpuConnectorFactory.Create(cameraToAEpuMapping.Type);

            this.logger.Information("Open {AEpuHostname} for user {WatchlistMemberFullName} ({WatchlistMemberID})", cameraToAEpuMapping.AEpuHostname, notification.WatchlistMemberFullName, notification.WatchlistMemberExternalId);

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

        private MyQMapping getCameraMapping(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var MyQMapping = this.configuration.GetSection("MyQMapping").Get<MyQMapping[]>();

            if (MyQMapping == null)
            {
                return null;
            }

            return MyQMapping
                        .Where(w => w.StreamId == streamGuid)
                        .FirstOrDefault();
        }

        private MyQMapping[] getAllCameraMappings()
        {
            return this.configuration
                            .GetSection("MyQMapping")
                            .Get<MyQMapping[]>();
        }
    }
}
