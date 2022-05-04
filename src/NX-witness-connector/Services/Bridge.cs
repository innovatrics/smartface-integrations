using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    public class Bridge : IBridge
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly INXWitnessAdapter nxWitnessAdapter;

        public Bridge(
            ILogger logger,
            IConfiguration configuration,
            INXWitnessAdapter nxWitnessAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.nxWitnessAdapter = nxWitnessAdapter ?? throw new ArgumentNullException(nameof(nxWitnessAdapter));
        }

        public async Task PushGenericEventAsync(
            DateTime? timestamp = null,
            string source = null,
            string caption = null,
            Guid? streamId = null
        )
        {
            var nxwitnessconnectorUserId = this.parseWatchlistId(notification.WatchlistMemberExternalId);

            if (nxwitnessconnectorUserId == null)
            {
                this.logger.Error("Failed to parse NXWitnessConnector user ID from notification. Source {source}", notification.WatchlistMemberExternalId);
                return;
            }

            var nxwitnessconnectorCameraId = this.convertToCameraId(notification.StreamId);

            if (nxwitnessconnectorCameraId == null)
            {
                this.logger.Information("Stream has not any mapping to NXWitnessConnector camera. StreamId {streamId}", notification.StreamId);
                return;
            }

            if (!this.authorizePolicy(notification))
            {
                this.logger.Warning("Notification does not pass policy. Quit");
                return;
            }

            await this.nxWitnessAdapter.OpenAsync(
                stationId: nxwitnessconnectorCameraId, 
                userId: nxwitnessconnectorUserId, 
                timestamp: notification.FaceDetectedAt, 
                score: notification.MatchResultScore,
                image: notification.CropImage    
            );
        }

        private int? parseWatchlistId(string input)
        {
            var match = Regex.Match(input, REGEX_USER_ID);

            if (match.Success)
            {
                return int.Parse(match.Groups[0].Value);
            }

            return null;
        }

        private string convertToCameraId(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var cameraMappings = this.configuration.GetSection("NXWitnessConnector:Cameras").Get<CameraMappingConfig[]>();

            if (cameraMappings == null)
            {
                return $"cam_{streamId}";
            }

            return cameraMappings
                        .Where(w => w.Source == streamGuid)
                        .Select(s => s.Target?.ToLower())
                        .FirstOrDefault();
        }

        private bool authorizePolicy(GrantedNotification notification)
        {
            if (!authorizeTimePolicy(notification))
            {
                this.logger.Warning($"Notification does not pass {nameof(AllowedTimeWindowPolicy)} policy. Quit");
                return false;
            }

            return true;
        }

        private bool authorizeTimePolicy(GrantedNotification notification)
        {
            var policy = this.configuration.GetSection("Policies:AllowedTimeWindow").Get<AllowedTimeWindowPolicy>();

            DateTime? from = null, to = null;

            if (!(policy?.Enabled ?? false))
            {
                return true;
            }

            var notificationLocalTime = notification.FaceDetectedAt.GetLocalDateTime();

            this.logger.Information("Notification FaceDetectAt: {detectAtUtc}, Local: {detectAtLocal}", notification.FaceDetectedAt, notificationLocalTime);

            var policyDay = policy.Days.ElementAtOrDefault((int)notificationLocalTime.DayOfWeek);

            this.logger.Information("Policy for day {notificationLocalTime.DayOfWeek} has configuration {@policy}", policyDay);

            if (policyDay == null)
            {
                return true;
            }

            if (policyDay.From != null)
            {
                from = DateTime.Now.Date.Add(policyDay.From);
            }

            if (policyDay.To != null)
            {
                to = DateTime.Now.Date.Add(policyDay.To);
            }

            if (from != null || to != null)
            {
                this.logger.Information("Allowed time window specified as {from} - {to}",
                    from?.ToString("HH-mm-ss"),
                    to?.ToString("HH-mm-ss")
                );
            }

            if (from != null && notificationLocalTime < from)
            {
                return false;
            }

            if (to != null && notificationLocalTime > to)
            {
                return false;
            }

            return true;
        }
    }
}