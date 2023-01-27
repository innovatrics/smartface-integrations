using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AEOSSync.Models;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public class Bridge : IBridge
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IAEOSSyncAdapter AEOSSync;

        public Bridge(
            ILogger logger,
            IConfiguration configuration,
            IAEOSSyncAdapter AEOSSync
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.AEOSSync = AEOSSync ?? throw new ArgumentNullException(nameof(AEOSSync));

            
        }

        public async Task ConnectToAEOS()
        {
            await this.AEOSSync.OpenAsync();
        }

/*
        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var AEOSSyncUserId = this.parseWatchlistId(notification.WatchlistMemberExternalId);

            if (AEOSSyncUserId == null)
            {
                this.logger.Error("Failed to parse AEOSSync user ID from notification. Source {source}", notification.WatchlistMemberExternalId);
                return;
            }

            var AEOSSyncCameraId = this.convertToCameraId(notification.StreamId);

            if (AEOSSyncCameraId == null)
            {
                this.logger.Information("Stream has not any mapping to AEOSSync camera. StreamId {streamId}", notification.StreamId);
                return;
            }

            await this.AEOSSync.OpenAsync(
                checkpoint_id: AEOSSyncCameraId, 
                ticket_id: AEOSSyncUserId
            );
        }
*/
        private string parseWatchlistId(string input)
        {
            var regexPattern = this.configuration.GetValue<string>("AEOSSync:UserIdRegEx");

            if (string.IsNullOrEmpty(regexPattern))
            {
                return input;
            }

            var match = Regex.Match(input, regexPattern);

            this.logger.Information("Matching RegEx pattern {regexPattern} with '{input}'", regexPattern, input);

            if (match.Success)
            {
                return match.Groups[0].Value;
            }

            return null;
        }

        private string convertToCameraId(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var cameraMappings = this.configuration.GetSection("AEOSSync:Cameras").Get<CameraMappingConfig[]>();

            if (cameraMappings == null)
            {
                return $"{streamId}";
            }

            return cameraMappings
                        .Where(w => w.Source == streamGuid)
                        .Select(s => s.Target)
                        .SingleOrDefault();
        }
    }
}