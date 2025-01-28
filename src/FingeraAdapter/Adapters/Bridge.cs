using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.FingeraAdapter.Models;

namespace Innovatrics.SmartFace.Integrations.FingeraAdapter
{
    public class Bridge : IBridge
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IFingeraAdapter fingeraAdapter;

        public Bridge(
            ILogger logger,
            IConfiguration configuration,
            IFingeraAdapter fingeraAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.fingeraAdapter = fingeraAdapter ?? throw new ArgumentNullException(nameof(fingeraAdapter));
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var fingeraUserId = this.parseWatchlistId(notification.WatchlistMemberExternalId);

            if (fingeraUserId == null)
            {
                this.logger.Error("Failed to parse Fingera user ID from notification. Source {source}", notification.WatchlistMemberExternalId);
                return;
            }

            var fingeraCameraId = this.convertToCameraId(notification.StreamId);

            if (fingeraCameraId == null)
            {
                this.logger.Information("Stream has not any mapping to Fingera camera. StreamId {streamId}", notification.StreamId);
                return;
            }

            if (!this.authorizePolicy(notification))
            {
                this.logger.Warning("Notification does not pass policy. Quit");
                return;
            }

            await this.fingeraAdapter.OpenAsync(
                stationId: fingeraCameraId, 
                userId: fingeraUserId, 
                timestamp: notification.GrpcSentAt, 
                score: notification.MatchResultScore,
                image: notification.CropImage    
            );
        }

        private int? parseWatchlistId(string input)
        {
            var regexPattern = this.configuration.GetValue<string>("Fingera:UserIdRegEx", @"(?<=innovatrics_)\d+");

            var match = Regex.Match(input, regexPattern);

            this.logger.Information("Matching RegEx pattern {regexPattern} with '{input}'", regexPattern, input);

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

            var cameraMappings = this.configuration.GetSection("Fingera:Cameras").Get<CameraMappingConfig[]>();

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

            var notificationLocalTime = notification.GrpcSentAt.GetLocalDateTime();

            this.logger.Information("Notification FaceDetectAt: {detectAtUtc}, Local: {detectAtLocal}", notification.GrpcSentAt, notificationLocalTime);

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