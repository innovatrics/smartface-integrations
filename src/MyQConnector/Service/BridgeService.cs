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
            this.myQConnectorFactory = MyQConnectorFactory ?? throw new ArgumentNullException(nameof(MyQConnectorFactory));
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var cameraToMyQMapping = this.getCameraMapping(notification.StreamId);

            if (cameraToMyQMapping == null)
            {
                this.logger.Information("Stream {streamId} has not any mapping to MyQ Print", notification.StreamId);
                return;
            }

            if (cameraToMyQMapping.PrinterSn == null)
            {
                this.logger.Information("Printer Serial Number needs to be mapped");
                return;
            }


            if (cameraToMyQMapping.WatchlistExternalIds != null)
            {
                if (cameraToMyQMapping.WatchlistExternalIds.Length > 0 && !cameraToMyQMapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
                {
                    this.logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
                    return;
                }
            }

            var myQConnector = this.myQConnectorFactory.Create(cameraToMyQMapping.Type);

            this.logger.Information("Opening printer: {PrinterSn} for user {WatchlistMemberDisplayName} ({WatchlistMemberID}) on streamID {StreamId}", cameraToMyQMapping.PrinterSn, notification.WatchlistMemberDisplayName, notification.WatchlistMemberExternalId, cameraToMyQMapping.StreamId);

            await myQConnector.OpenAsync(
                myqPrinter: cameraToMyQMapping.PrinterSn,
                myqStreamId: cameraToMyQMapping.StreamId,
                watchlistMemberId: notification.WatchlistMemberId
            );
        }


        private MyQMapping getCameraMapping(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var myQMapping = this.configuration.GetSection("MyQMapping").Get<MyQMapping[]>();

            if (myQMapping == null)
            {
                return null;
            }

            return myQMapping
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
