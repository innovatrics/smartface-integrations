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
        private readonly StreamConfig[] _allStreamConfigs;

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

            _allStreamConfigs = configuration.GetSection("StreamConfig").Get<StreamConfig[]>() ?? [];
            _allStreamConfigs = _allStreamConfigs.Where(x => x.Enabled).ToArray();

            if (!_allStreamConfigs.Any())
            {
                throw new InvalidOperationException("No enabled connectors configured in StreamConfig");
            }
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            var streamConfigs = GetAccessConnectorConfigsForStream(notification.StreamId);

            if (streamConfigs.Length == 0)
            {
                _log.Warning("Granted notification for Stream {StreamId} has no AccessConnector configuration", notification.StreamId);
                return;
            }

            foreach (var streamConfig in streamConfigs)
            {
                _log.Debug("Handling access for connector of type {ConnectorType}", streamConfig.Type);

                var watchlistExternalIds = streamConfig.WatchlistExternalIds;

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

                var accessControlConnector = _accessControlConnectorFactory.Create(streamConfig.Type);
                string accessControlUser = null;

                if (streamConfig.UserResolver != null)
                {
                    var userResolver = _userResolverFactory.Create(streamConfig.UserResolver);

                    accessControlUser = await userResolver.ResolveUserAsync(notification);

                    _log.Debug("Resolved {WlMemberId} to {AccessControlUser}", notification.WatchlistMemberId, accessControlUser);

                    if (accessControlUser == null)
                    {
                        continue;
                    }
                }

                await accessControlConnector.OpenAsync(streamConfig, accessControlUser);

                if (streamConfig.NextCallDelayMs is > 0)
                {
                    _log.Information("Delay next call for {NextCallDelayMs} ms", streamConfig.NextCallDelayMs);

                    await Task.Delay(streamConfig.NextCallDelayMs.Value);
                }
            }
        }

        public async Task SendKeepAliveSignalAsync()
        {
            var uniqueAccessControls = _allStreamConfigs
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

        private StreamConfig[] GetStreamConfigsForStream(string streamIdStr)
        {
            if (!Guid.TryParse(streamIdStr, out var streamId))
            {
                throw new InvalidOperationException($"{nameof(streamIdStr)} is expected as GUID");
            }

            return _allStreamConfigs.Where(w => w.StreamId == streamId).ToArray();
        }
    }
}
