using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Serilog;

using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Models;
using Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Factories;


namespace Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Services
{
    public class BridgeService : IBridgeService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IMyQConnectorFactory MyQConnectorFactory;

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

            var MyQConnector = this.MyQConnectorFactory.Create(cameraToMyQMapping.Type);

            this.logger.Information("Open {MyQHostname} printer: {PrinterSn} for user {WatchlistMemberFullName} ({WatchlistMemberID})", cameraToMyQMapping.MyQHostname, cameraToMyQMapping.PrinterSn, notification.WatchlistMemberFullName, notification.WatchlistMemberExternalId);

            await MyQConnector.OpenAsync(
                myqHostname: cameraToMyQMapping.MyQHostname,
                myqPort: cameraToMyQMapping.MyQPort
            );
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
