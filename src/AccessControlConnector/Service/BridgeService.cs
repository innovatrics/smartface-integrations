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
        private readonly ILogger _log;
        private readonly AccessControlConnectorFactory _accessControlConnectorFactory;
        private readonly IUserResolverFactory _userResolverFactory;
        private readonly AccessControlMapping[] _allAccessConnectorConfigs;

        public BridgeService(
            ILogger log,
            IConfiguration configuration,
            AccessControlConnectorFactory accessControlConnectorFactory,
            IUserResolverFactory userResolverFactory
        )
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _accessControlConnectorFactory = accessControlConnectorFactory ?? throw new ArgumentNullException(nameof(accessControlConnectorFactory));
            _userResolverFactory = userResolverFactory ?? throw new ArgumentNullException(nameof(userResolverFactory));

            _allAccessConnectorConfigs = configuration.GetSection("AccessControlMapping").Get<AccessControlMapping[]>() ?? [];
            _allAccessConnectorConfigs = _allAccessConnectorConfigs.Where(x => x.Enabled).ToArray();

            if (!_allAccessConnectorConfigs.Any())
            {
                throw new InvalidOperationException("No enabled connectors configured in AccessControlMapping");
            }
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            var streamAccessConnectorsConfigs = GetAccessConnectorConfigsForStream(notification.StreamId);

            if (streamAccessConnectorsConfigs.Length == 0)
            {
                _log.Warning("Granted notification for Stream {StreamId} has no AccessConnector configuration", notification.StreamId);
                return;
            }

            foreach (var streamAccessConnectorsConfig in streamAccessConnectorsConfigs)
            {
                _log.Debug("Handling access for connector of type {ConnectorType}", streamAccessConnectorsConfig.Type);

                var watchlistExternalIds = streamAccessConnectorsConfig.WatchlistExternalIds;

                if (watchlistExternalIds != null)
                {
                    if (watchlistExternalIds.Length > 0 &&
                        !watchlistExternalIds.Contains(notification.WatchlistExternalId))
                    {
                        _log.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {StreamId}",
                            notification.WatchlistExternalId, notification.StreamId);

                        continue;
                    }
                }

                var accessControlConnector = _accessControlConnectorFactory.Create(streamAccessConnectorsConfig.Type);
                string accessControlUser = null;

                if (streamAccessConnectorsConfig.UserResolver != null)
                {
                    var userResolver = _userResolverFactory.Create(streamAccessConnectorsConfig.UserResolver);

                    accessControlUser = await userResolver.ResolveUserAsync(notification);

                    _log.Debug("Resolved {WlMemberId} to {AccessControlUser}", notification.WatchlistMemberId, accessControlUser);

                    if (accessControlUser == null)
                    {
                        continue;
                    }
                }

                await accessControlConnector.OpenAsync(streamAccessConnectorsConfig, accessControlUser);

                if (streamAccessConnectorsConfig.NextCallDelayMs is > 0)
                {
                    _log.Information("Delay next call for {NextCallDelayMs} ms", streamAccessConnectorsConfig.NextCallDelayMs);

                    await Task.Delay(streamAccessConnectorsConfig.NextCallDelayMs.Value);
                }
            }
        }

        public async Task SendKeepAliveSignalAsync()
        {
            var uniqueAccessControls = _allAccessConnectorConfigs
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

        private AccessControlMapping[] GetAccessConnectorConfigsForStream(string streamIdStr)
        {
            if (!Guid.TryParse(streamIdStr, out var streamId))
            {
                throw new InvalidOperationException($"{nameof(streamIdStr)} is expected as GUID");
            }

            return _allAccessConnectorConfigs.Where(w => w.StreamId == streamId).ToArray();
        }
    }
}
