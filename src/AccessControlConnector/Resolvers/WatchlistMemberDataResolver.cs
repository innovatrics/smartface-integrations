using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers
{
    /// <summary>
    /// General resolver that packages WatchlistMember data from notifications
    /// Can return either just the MemberId or a full data package with labels
    /// </summary>
    public class WatchlistMemberDataResolver : IUserResolver
    {
        private readonly ILogger _logger;
        private readonly string[] _labelKeys;
        private readonly bool _includeLabels;

        public WatchlistMemberDataResolver(ILogger logger, params string[] labelKeys)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _labelKeys = labelKeys ?? Array.Empty<string>();
            _includeLabels = _labelKeys.Length > 0;
        }

        public Task<string> ResolveUserAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            _logger.Information("Resolving WatchlistMember data for {watchlistMemberName}", notification.WatchlistMemberDisplayName);

            // If no labels requested, just return the MemberId
            if (!_includeLabels)
            {
                _logger.Information("Returning WatchlistMemberId: {MemberId}", notification.WatchlistMemberId);
                return Task.FromResult(notification.WatchlistMemberId);
            }

            // Otherwise, package MemberId + requested labels as JSON
            var data = new System.Collections.Generic.Dictionary<string, string>
            {
                { "MemberId", notification.WatchlistMemberId }
            };

            // Add requested labels
            foreach (var labelKey in _labelKeys)
            {
                var value = GetLabelValue(notification, labelKey);
                data[labelKey] = value;
            }

            var json = JsonSerializer.Serialize(data);
            _logger.Information("Resolved WatchlistMember data: {Data}", json);

            return Task.FromResult(json);
        }

        private string GetLabelValue(GrantedNotification notification, string labelKey)
        {
            if (notification.WatchlistMemberLabels == null || notification.WatchlistMemberLabels.Length == 0)
            {
                return null;
            }

            var label = notification.WatchlistMemberLabels.FirstOrDefault(l => 
                l.Key.Equals(labelKey, StringComparison.OrdinalIgnoreCase));

            return label.Value;
        }
    }
}

