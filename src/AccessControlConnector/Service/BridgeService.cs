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
        private readonly AccessControlMapping[] _allCamerasMappings;

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

            _allCamerasMappings = GetAllCameraMappings();
        }

        public async Task ProcessGrantedNotificationAsync(AccessControlMapping mapping, GrantedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            _logger.Information("Handling mapping {type}", mapping.Type);

            if (mapping.WatchlistExternalIds != null)
            {
                if (mapping.WatchlistExternalIds.Length > 0 && !mapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
                {
                    _logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
                    return;
                }
            }

            string accessControlUser = null;

            var accessControlConnector = _accessControlConnectorFactory.Create(mapping.Type);

            if (mapping.UserResolver != null)
            {
                var userResolver = _userResolverFactory.Create(mapping.UserResolver);

                accessControlUser = await userResolver.ResolveUserAsync(notification);

                _logger.Information("Resolved {wlMemberId} to {accessControlUser}", notification.WatchlistMemberId, accessControlUser);

                if (accessControlUser == null)
                {
                    return;
                }
            }

            await accessControlConnector.OpenAsync(mapping, accessControlUser);

            if (mapping.NextCallDelayMs != null &&
                mapping.NextCallDelayMs > 0)
            {
                _logger.Information("Delay next call for {nextCallDelayMs} ms", mapping.NextCallDelayMs);

                await Task.Delay(mapping.NextCallDelayMs.Value);
            }
        }

        public async Task ProcessBlockedNotificationAsync(AccessControlMapping mapping, BlockedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            _logger.Information("Handling mapping {type}", mapping.Type);

            if (mapping.WatchlistExternalIds != null)
            {
                if (mapping.WatchlistExternalIds.Length > 0 && !mapping.WatchlistExternalIds.Contains(notification.WatchlistId))
                {
                    _logger.Warning("Watchlist {watchlistId} has no right to enter through this gate {streamId}.", notification.WatchlistId, notification.StreamId);
                    return;
                }
            }

            string accessControlUser = null;

            var accessControlConnector = _accessControlConnectorFactory.Create(mapping.Type);

            if (mapping.UserResolver != null)
            {
                var userResolver = _userResolverFactory.Create(mapping.UserResolver);

                accessControlUser = await userResolver.ResolveUserAsync(notification);

                _logger.Information("Resolved {wlMemberId} to {accessControlUser}", notification.WatchlistMemberId, accessControlUser);

                if (accessControlUser == null)
                {
                    return;
                }
            }

            await accessControlConnector.BlockAsync(mapping, accessControlUser);

            if (mapping.NextCallDelayMs != null &&
                mapping.NextCallDelayMs > 0)
            {
                _logger.Information("Delay next call for {nextCallDelayMs} ms", mapping.NextCallDelayMs);

                await Task.Delay(mapping.NextCallDelayMs.Value);
            }
        }

        public async Task ProcessDeniedNotificationAsync(AccessControlMapping mapping, DeniedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            _logger.Information("Handling mapping {type}", mapping.Type);

            var accessControlConnector = _accessControlConnectorFactory.Create(mapping.Type);

            await accessControlConnector.DenyAsync(mapping);

            if (mapping.NextCallDelayMs != null &&
                mapping.NextCallDelayMs > 0)
            {
                _logger.Information("Delay next call for {nextCallDelayMs} ms", mapping.NextCallDelayMs);

                await Task.Delay(mapping.NextCallDelayMs.Value);
            }
        }

        public async Task SendKeepAliveSignalAsync()
        {
            var cameraToAccessControlMapping = GetAllCameraMappings();

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

            return _allCamerasMappings
                        .Where(w => w.StreamId == streamGuid)
                        .ToArray();
        }

        private AccessControlMapping[] GetAllCameraMappings()
        {
            var mappings = _configuration
                                .GetSection("AccessControlMapping")
                                .Get<AccessControlMapping[]>();

            if (mappings == null)
            {
                mappings = new AccessControlMapping[] { };
            }

            return mappings;
        }
    }
}