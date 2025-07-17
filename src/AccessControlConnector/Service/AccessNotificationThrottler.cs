using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories;
using System.Threading;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public delegate Task AccessControlEventHandler<T>(AccessControlMapping mapping, T notification);

    public class AccessNotificationThrottler
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private readonly IUserResolverFactory _userResolverFactory;
        private readonly AccessControlMapping[] _allMappings;
        private readonly TimeSpan _throttleDuration;

        private readonly ConcurrentDictionary<string, DateTime> _lastExecuted = new();

        public event AccessControlEventHandler<GrantedNotification> OnGranted;
        public event AccessControlEventHandler<DeniedNotification> OnDenied;
        public event AccessControlEventHandler<BlockedNotification> OnBlocked;

        public AccessNotificationThrottler(
            ILogger logger,
            IConfiguration config,
            IUserResolverFactory userResolverFactory)
        {
            _logger = logger;
            _config = config;
            _userResolverFactory = userResolverFactory;

            _throttleDuration = TimeSpan.FromMilliseconds(config.GetValue<int>("Config:ThrottleWindow", 300));
            _allMappings = config.GetSection("AccessControlMapping").Get<AccessControlMapping[]>() ?? Array.Empty<AccessControlMapping>();
        }

        public async Task HandleGrantedAsync(GrantedNotification notification)
        {
            await HandleAsync(notification.StreamId, notification.WatchlistExternalId, AccessEventType.Granted, notification);
        }

        public async Task HandleDeniedAsync(DeniedNotification notification)
        {
            await HandleAsync(notification.StreamId, null, AccessEventType.Denied, notification);
        }

        public async Task HandleBlockedAsync(BlockedNotification notification)
        {
            await HandleAsync(notification.StreamId, notification.WatchlistId, AccessEventType.Blocked, notification);
        }

        private async Task HandleAsync<T>(string streamId, string watchlistExternalId, AccessEventType eventType, T notification)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                _logger.Warning("Invalid StreamId format: {streamId}", streamId);
                return;
            }

            var mappingsPerStream = _allMappings.Where(m => m.StreamId == streamGuid).ToArray();

            if (mappingsPerStream.Length == 0)
            {
                _logger.Warning("No mappings found for stream {streamId}", streamId);
                return;
            }

            // Group mappings by GroupName or StreamId fallback
            var mappingsByGroup = mappingsPerStream
                .GroupBy(m => m.Group ?? m.StreamId.ToString())
                .ToList();

            foreach (var group in mappingsByGroup)
            {
                var key = group.Key;
                var now = DateTime.UtcNow;

                if (_throttleDuration > TimeSpan.Zero &&
                    _lastExecuted.TryGetValue(key, out var lastTime) &&
                    (now - lastTime) < _throttleDuration)
                {
                    _logger.Debug("Throttling key {key} for {ms}ms", key, (now - lastTime).TotalMilliseconds);
                    continue;
                }

                var relevantMappings = group
                    .Where(m =>
                        m.WatchlistExternalIds == null ||
                        m.WatchlistExternalIds.Length == 0 ||
                        string.IsNullOrEmpty(watchlistExternalId) ||
                        m.WatchlistExternalIds.Contains(watchlistExternalId))
                    .ToList();

                if (relevantMappings.Count == 0)
                {
                    _logger.Debug("No relevant mappings remain for key {key}", key);
                    continue;
                }

                _logger.Information("Executing {count} mapping(s) for key {key} and event {eventType}", relevantMappings.Count, key, eventType);

                foreach (var mapping in relevantMappings)
                {
                    await RaiseEventAsync(eventType, mapping, notification);
                }

                // Record execution after full group is handled
                _lastExecuted[key] = now;
            }
        }

        private async Task RaiseEventAsync<T>(AccessEventType type, AccessControlMapping mapping, T notification)
        {
            switch (type)
            {
                case AccessEventType.Granted:
                    if (OnGranted != null)
                        await OnGranted.Invoke(mapping, notification as GrantedNotification);
                    break;
                case AccessEventType.Denied:
                    if (OnDenied != null)
                        await OnDenied.Invoke(mapping, notification as DeniedNotification);
                    break;
                case AccessEventType.Blocked:
                    if (OnBlocked != null)
                        await OnBlocked.Invoke(mapping, notification as BlockedNotification);
                    break;
            }
        }
    }

    public enum AccessEventType
    {
        Granted,
        Denied,
        Blocked
    }
}