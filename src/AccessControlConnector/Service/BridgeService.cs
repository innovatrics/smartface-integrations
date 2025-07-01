using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories;
using System.Threading;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public class BridgeService : IBridgeService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IAccessControlConnectorFactory _accessControlConnectorFactory;
        private readonly IUserResolverFactory _userResolverFactory;

        public BridgeService(
            ILogger logger,
            IConfiguration configuration,
            IAccessControlConnectorFactory accessControlConnectorFactory,
            IUserResolverFactory userResolverFactory
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _accessControlConnectorFactory = accessControlConnectorFactory ?? throw new ArgumentNullException(nameof(accessControlConnectorFactory));
            _userResolverFactory = userResolverFactory ?? throw new ArgumentNullException(nameof(userResolverFactory));
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            var cameraToAccessControlMappings = GetCameraMappings(notification.StreamId);

            if (cameraToAccessControlMappings.Length == 0)
            {
                _logger.Warning("Stream {streamId} has not any mapping to AccessControl", notification.StreamId);
                return;
            }

            foreach (var cameraToAccessControlMapping in cameraToAccessControlMappings)
            {
                _logger.Warning("Handling mapping {type}", cameraToAccessControlMapping.Type);

                if (cameraToAccessControlMapping.WatchlistExternalIds != null)
                {
                    if (cameraToAccessControlMapping.WatchlistExternalIds.Length > 0 && !cameraToAccessControlMapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
                    {
                        _logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
                        continue;
                    }
                }

                string accessControlUser = null;

                var accessControlConnector = _accessControlConnectorFactory.Create(cameraToAccessControlMapping.Type);

                if (cameraToAccessControlMapping.UserResolver != null)
                {
                    var userResolver = _userResolverFactory.Create(cameraToAccessControlMapping.UserResolver);

                    accessControlUser = await userResolver.ResolveUserAsync(notification);

                    _logger.Information("Resolved {wlMemberId} to {accessControlUser}", notification.WatchlistMemberId, accessControlUser);

                    if (accessControlUser == null)
                    {
                        continue;
                    }
                }

                await accessControlConnector.OpenAsync(cameraToAccessControlMapping, accessControlUser);

                if (cameraToAccessControlMapping.NextCallDelayMs != null &&
                    cameraToAccessControlMapping.NextCallDelayMs > 0)
                {
                    _logger.Information("Delay next call for {nextCallDelayMs} ms", cameraToAccessControlMapping.NextCallDelayMs);

                    await Task.Delay(cameraToAccessControlMapping.NextCallDelayMs.Value);
                }
            }
        }

        public async Task ProcessBlockedNotificationAsync(BlockedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            var cameraToAccessControlMappings = GetCameraMappings(notification.StreamId);

            if (cameraToAccessControlMappings.Length == 0)
            {
                _logger.Warning("Stream {streamId} has not any mapping to AccessControl", notification.StreamId);
                return;
            }

            foreach (var cameraToAccessControlMapping in cameraToAccessControlMappings)
            {
                _logger.Warning("Handling mapping {type}", cameraToAccessControlMapping.Type);

                if (cameraToAccessControlMapping.WatchlistExternalIds != null)
                {
                    if (cameraToAccessControlMapping.WatchlistExternalIds.Length > 0 && !cameraToAccessControlMapping.WatchlistExternalIds.Contains(notification.WatchlistId))
                    {
                        _logger.Warning("Watchlist {watchlistId} has no right to enter through this gate {streamId}.", notification.WatchlistId, notification.StreamId);
                        continue;
                    }
                }

                string accessControlUser = null;

                var accessControlConnector = _accessControlConnectorFactory.Create(cameraToAccessControlMapping.Type);

                if (cameraToAccessControlMapping.UserResolver != null)
                {
                    var userResolver = _userResolverFactory.Create(cameraToAccessControlMapping.UserResolver);

                    accessControlUser = await userResolver.ResolveUserAsync(notification);

                    _logger.Information("Resolved {wlMemberId} to {accessControlUser}", notification.WatchlistMemberId, accessControlUser);

                    if (accessControlUser == null)
                    {
                        continue;
                    }
                }

                await accessControlConnector.BlockAsync(cameraToAccessControlMapping, accessControlUser);

                if (cameraToAccessControlMapping.NextCallDelayMs != null &&
                    cameraToAccessControlMapping.NextCallDelayMs > 0)
                {
                    _logger.Information("Delay next call for {nextCallDelayMs} ms", cameraToAccessControlMapping.NextCallDelayMs);

                    await Task.Delay(cameraToAccessControlMapping.NextCallDelayMs.Value);
                }
            }
        }

        public async Task ProcessDeniedNotificationAsync(DeniedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            var cameraToAccessControlMappings = GetCameraMappings(notification.StreamId);

            if (cameraToAccessControlMappings.Length == 0)
            {
                _logger.Warning("Stream {streamId} has not any mapping to AccessControl", notification.StreamId);
                return;
            }

            foreach (var cameraToAccessControlMapping in cameraToAccessControlMappings)
            {
                _logger.Warning("Handling mapping {type}", cameraToAccessControlMapping.Type);

                var accessControlConnector = _accessControlConnectorFactory.Create(cameraToAccessControlMapping.Type);

                await accessControlConnector.DenyAsync(cameraToAccessControlMapping);

                if (cameraToAccessControlMapping.NextCallDelayMs != null &&
                    cameraToAccessControlMapping.NextCallDelayMs > 0)
                {
                    _logger.Information("Delay next call for {nextCallDelayMs} ms", cameraToAccessControlMapping.NextCallDelayMs);

                    await Task.Delay(cameraToAccessControlMapping.NextCallDelayMs.Value);
                }
            }
        }

        public async Task SendKeepAliveSignalAsync()
        {
            var cameraToAccessControlMapping = getAllCameraMappings();

            if (cameraToAccessControlMapping == null)
            {
                _logger.Warning("No mapping to AccessControl configured");
                return;
            }

            var uniqueAccessControls = cameraToAccessControlMapping
                                                .GroupBy(g => new
                                                {
                                                    g.Schema,
                                                    g.Type,
                                                    g.Host,
                                                    g.Port,
                                                    g.Username,
                                                    g.Password
                                                })
                                                .ToArray();

            foreach (var uniqueMapping in uniqueAccessControls)
            {
                var accessControlConnector = _accessControlConnectorFactory.Create(uniqueMapping.Key.Type);

                await accessControlConnector.SendKeepAliveAsync(
                    schema: uniqueMapping.Key.Schema,
                    host: uniqueMapping.Key.Host,
                    port: uniqueMapping.Key.Port,
                    channel: uniqueMapping.First().Channel,
                    username: uniqueMapping.Key.Username,
                    password: uniqueMapping.Key.Password
                );
            }
        }

        private AccessControlMapping[] GetCameraMappings(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var accessControlMapping = _configuration.GetSection("AccessControlMapping").Get<AccessControlMapping[]>();

            if (accessControlMapping == null)
            {
                return new AccessControlMapping[] { };
            }

            return accessControlMapping
                        .Where(w => w.StreamId == streamGuid)
                        .ToArray();
        }

        private AccessControlMapping[] getAllCameraMappings()
        {
            return _configuration
                            .GetSection("AccessControlMapping")
                            .Get<AccessControlMapping[]>();
        }
    }
}
