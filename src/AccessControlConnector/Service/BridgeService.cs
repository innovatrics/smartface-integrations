using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public class BridgeService : IBridgeService
    {
        private readonly ILogger _logger;
        private readonly AccessControlConnectorFactory _accessControlConnectorFactory;
        private readonly IUserResolverFactory _userResolverFactory;
        private readonly AccessControlMapping[] _allAcMappings;

        public BridgeService(
            ILogger logger,
            IConfiguration configuration,
            AccessControlConnectorFactory accessControlConnectorFactory,
            IUserResolverFactory userResolverFactory
        )
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accessControlConnectorFactory = accessControlConnectorFactory ?? throw new ArgumentNullException(nameof(accessControlConnectorFactory));
            _userResolverFactory = userResolverFactory ?? throw new ArgumentNullException(nameof(userResolverFactory));

            _allAcMappings = configuration.GetSection("AccessControlMapping").Get<AccessControlMapping[]>() ?? [];

            if (!_allAcMappings.Any())
            {
                throw new InvalidOperationException("No connectors configured in mappings");
            }
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            var streamMappings = GetMappingsByStreamId(notification.StreamId);

            if (streamMappings.Length == 0)
            {
                _logger.Warning("Stream {streamId} has not any mapping to AccessControl", notification.StreamId);
                return;
            }

            foreach (var acStreamMapping in streamMappings)
            {
                _logger.Debug("Handling mapping for connector type {ConnectorType}", acStreamMapping.Type);

                var watchlistExternalIds = acStreamMapping.WatchlistExternalIds;

                if (watchlistExternalIds != null)
                {
                    if (watchlistExternalIds.Length > 0 &&
                        !watchlistExternalIds.Contains(notification.WatchlistExternalId))
                    {
                        _logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.",
                            notification.WatchlistExternalId, notification.StreamId);

                        continue;
                    }
                }

                string accessControlUser = null;

                var accessControlConnector = _accessControlConnectorFactory.Create(acStreamMapping.Type);

                if (acStreamMapping.UserResolver != null)
                {
                    var userResolver = _userResolverFactory.Create(acStreamMapping.UserResolver);

                    accessControlUser = await userResolver.ResolveUserAsync(notification);

                    _logger.Debug("Resolved {wlMemberId} to {accessControlUser}", notification.WatchlistMemberId, accessControlUser);

                    if (accessControlUser == null)
                    {
                        continue;
                    }
                }

                await accessControlConnector.OpenAsync(acStreamMapping, accessControlUser);

                if (acStreamMapping.NextCallDelayMs is > 0)
                {
                    _logger.Information("Delay next call for {nextCallDelayMs} ms", acStreamMapping.NextCallDelayMs);

                    await Task.Delay(acStreamMapping.NextCallDelayMs.Value);
                }
            }
        }

        public async Task SendKeepAliveSignalAsync()
        {
            var uniqueAccessControls = _allAcMappings
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

        private AccessControlMapping[] GetMappingsByStreamId(string streamIdStr)
        {
            if (!Guid.TryParse(streamIdStr, out var streamId))
            {
                throw new InvalidOperationException($"{nameof(streamIdStr)} is expected as GUID");
            }

            return _allAcMappings.Where(w => w.StreamId == streamId).ToArray();
        }
    }
}
