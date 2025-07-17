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

            foreach (var mapping in mappingsPerStream)
            {
                if (mapping.WatchlistExternalIds?.Length > 0 &&
                    !string.IsNullOrEmpty(watchlistExternalId) &&
                    !mapping.WatchlistExternalIds.Contains(watchlistExternalId))
                {
                    _logger.Information("Skipping mapping: {watchlistExternalId} is not allowed for {streamId}", watchlistExternalId, streamId);
                    continue;
                }

                var key = mapping.Group ?? $"{mapping?.StreamId}";

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (_throttleDuration > TimeSpan.Zero)
                {
                    var now = DateTime.UtcNow;
                    if (_lastExecuted.TryGetValue(key, out var lastTime))
                    {
                        var timeDiff = now - lastTime;
                        if (timeDiff < _throttleDuration)
                        {
                            _logger.Debug("Throttling key {key} for {timeDiff}ms", key, timeDiff.TotalMilliseconds);
                            continue;
                        }
                    }

                    _lastExecuted[key] = now;
                }

                _logger.Information("Emitting {eventType} event for {key}", eventType, key);
                await RaiseEventAsync(eventType, mapping, notification);
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