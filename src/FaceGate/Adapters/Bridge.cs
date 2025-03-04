using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.FaceGate.Models;

namespace Innovatrics.SmartFace.Integrations.FaceGate
{
    public class Bridge : IBridge
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IFaceGateAdapter faceGate;

        public Bridge(
            ILogger logger,
            IConfiguration configuration,
            IFaceGateAdapter faceGate
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.faceGate = faceGate ?? throw new ArgumentNullException(nameof(faceGate));
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var faceGateUserId = this.parseWatchlistId(notification.WatchlistMemberExternalId);

            if (faceGateUserId == null)
            {
                this.logger.Error("Failed to parse FaceGate user ID from notification. Source {source}", notification.WatchlistMemberExternalId);
                return;
            }

            var faceGateCameraId = this.convertToCameraId(notification.StreamId);

            if (faceGateCameraId == null)
            {
                this.logger.Information("Stream has not any mapping to FaceGate camera. StreamId {streamId}", notification.StreamId);
                return;
            }

            await this.faceGate.OpenAsync(
                checkpoint_id: faceGateCameraId, 
                ticket_id: faceGateUserId
            );
        }

        private string parseWatchlistId(string input)
        {
            var regexPattern = this.configuration.GetValue<string>("FaceGate:UserIdRegEx");

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

            var cameraMappings = this.configuration.GetSection("FaceGate:Cameras").Get<CameraMappingConfig[]>();

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