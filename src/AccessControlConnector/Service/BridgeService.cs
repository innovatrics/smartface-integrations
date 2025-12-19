using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
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
        private static readonly SemaphoreSlim _connectorSemaphore = new(5);

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

            _allStreamConfigs = LoadStreamConfig(configuration);
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            var streamConfigs = GetStreamConfigsForStream(notification.StreamId);

            if (streamConfigs.Length == 0)
            {
                _log.Warning("Granted notification for Stream {StreamId} has no AccessConnector configuration", notification.StreamId);
                return;
            }

            foreach (var streamConfig in streamConfigs)
            {
                _log.Debug("Handling access for connector of type {ConnectorType}", streamConfig.Type);

                if (streamConfig.Async)
                {
                    _ = Task.Run(async () =>
                    {
                        await _connectorSemaphore.WaitAsync();
                        try
                        {
                            await ExecuteConnectorAsync(config, notification);
                        }
                        finally
                        {
                            _connectorSemaphore.Release();
                        }
                    });
                }
                else
                {
                    await ExecuteConnectorAsync(streamConfig, notification);
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

        private StreamConfig[] GetStreamConfigsForStream(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            return _allStreamConfigs
                        .Where(w => w.StreamId == streamGuid)
                        .ToArray();
        }

        private StreamConfig[] LoadStreamConfig(IConfiguration configuration)
        {
            StreamConfig[] streamConfigs;

            var streamConfigPath = configuration.GetValue<string>("StreamConfigPath");
            if (!string.IsNullOrWhiteSpace(streamConfigPath))
            {
                if (!File.Exists(streamConfigPath))
                {
                    throw new FileNotFoundException($"StreamConfigJson file not found: {streamConfigPath}");
                }

                var jsonContent = File.ReadAllText(streamConfigPath);
                streamConfigs = JsonConvert.DeserializeObject<StreamConfig[]>(jsonContent) ?? [];
            }
            else
            {
                streamConfigs = configuration.GetSection("StreamConfig").Get<StreamConfig[]>() ?? [];
            }

            streamConfigs = streamConfigs.Where(x => x.Enabled).ToArray();

            if (!streamConfigs.Any())
            {
                throw new InvalidOperationException("No enabled connectors configured in StreamConfig");
            }

            foreach (var streamConfig in streamConfigs)
            {
                _log.Information("Stream [{streamId}] for {Type} with face: {FaceModalityEnabled} palm: {PalmModalityEnabled} opticalCode: {OpticalCodeModalityEnabled}", streamConfig.StreamId, streamConfig.Type, streamConfig.FaceModalityEnabled ? 1 : 0, streamConfig.PalmModalityEnabled ? 1 : 0, streamConfig.OpticalCodeModalityEnabled ? 1 : 0);
            }

            return streamConfigs;
        }

        private async Task ExecuteConnectorAsync(StreamConfig streamConfig, GrantedNotification notification)
        {
            bool modalityEnabled = notification.Modality switch
            {
                Modality.Face => streamConfig.FaceModalityEnabled,
                Modality.Palm => streamConfig.PalmModalityEnabled,
                Modality.OpticalCode => streamConfig.OpticalCodeModalityEnabled,
                _ => false
            };

            if (!modalityEnabled)
            {
                _log.Warning("Stream config does not apply to modality {Modality} for Stream {StreamId}", notification.Modality, notification.StreamId);
                return;
            }

            var watchlistExternalIds = streamConfig.WatchlistExternalIds;

            if (watchlistExternalIds != null)
            {
                if (watchlistExternalIds.Length > 0 &&
                    !watchlistExternalIds.Contains(notification.WatchlistExternalId))
                {
                    _log.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {StreamId}",
                        notification.WatchlistExternalId, notification.StreamId);

                    return;
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
                    return;
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
}
