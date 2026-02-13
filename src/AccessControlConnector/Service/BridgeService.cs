using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AccessControlConnector.Connectors;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Telemetry;

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

            _allStreamConfigs = LoadStreamConfig(configuration);
        }

        public async Task ProcessGrantedNotificationAsync(GrantedNotification grantedNotification)
        {
            ArgumentNullException.ThrowIfNull(grantedNotification);

            using var activity = AccessControlTelemetry.ActivitySource.StartActivity(
                AccessControlTelemetry.GrantedOperationName,
                ActivityKind.Consumer,
                parentContext: grantedNotification.ActivityContext);

            activity?.SetTag(AccessControlTelemetry.StreamIdAttribute, grantedNotification.StreamId);

            try
            {
                var streamConfigs = GetStreamConfigsForStream(grantedNotification.StreamId);

                // harmless dummy connector for easier local debug of connector infra
                if (DummyConnector.Enabled)
                {
                    streamConfigs.Add(new StreamConfig
                    {
                        Enabled = true,
                        Async = true,
                        Type = AccessControlConnectorTypes.DUMMY_CONNECTOR,
                        StreamId = Guid.Parse(grantedNotification.StreamId)
                    });
                }

                if (streamConfigs.Count == 0)
                {
                    _log.Warning("Granted grantedNotification for Stream {StreamId} has no AccessConnector configuration", grantedNotification.StreamId);
                    return;
                }

                foreach (var streamConfig in streamConfigs)
                {
                    _log.Debug("Handling access for connector of type {ConnectorType}", streamConfig.Type);

                    if (streamConfig.Async)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ExecuteConnectorAsync(streamConfig, grantedNotification);
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, "Failed to execute connector for stream {StreamId}", grantedNotification.StreamId);
                            }
                        });
                    }
                    else
                    {
                        await ExecuteConnectorAsync(streamConfig, grantedNotification);
                    }
                }
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
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

        private List<StreamConfig> GetStreamConfigsForStream(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            return _allStreamConfigs
                        .Where(w => w.StreamId == streamGuid)
                        .ToList();
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

            foreach (var streamConfig in streamConfigs)
            {
                _log.Information("Stream [{streamId}] for {Type} with face: {FaceModalityEnabled} palm: {PalmModalityEnabled} opticalCode: {OpticalCodeModalityEnabled}", streamConfig.StreamId, streamConfig.Type, streamConfig.FaceModalityEnabled ? 1 : 0, streamConfig.PalmModalityEnabled ? 1 : 0, streamConfig.OpticalCodeModalityEnabled ? 1 : 0);
            }

            return streamConfigs;
        }

        private async Task ExecuteConnectorAsync(StreamConfig streamConfig, GrantedNotification notification)
        {
            using var activity = AccessControlTelemetry.ActivitySource.StartActivity(
                AccessControlTelemetry.ConnectorHandleOperationName);

            activity?.SetTag(AccessControlTelemetry.ConnectorNameAttribute, streamConfig.Type);
            activity?.SetTag(AccessControlTelemetry.ConnectorTypeAttribute, GetConnectorType(streamConfig.Type));
            activity?.SetTag(AccessControlTelemetry.StreamIdAttribute, notification.StreamId);

            try
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
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }

        private static string GetConnectorType(string connectorType)
        {
            if (string.IsNullOrEmpty(connectorType))
            {
                return "Unknown";
            }

            var normalizedType = connectorType.ToUpperInvariant();

            if (normalizedType.Contains("INNERRANGE") || normalizedType.Contains("INTEGRITI"))
            {
                return "InnerRange";
            }
            if (normalizedType.Contains("AXIS"))
            {
                return "AXIS";
            }
            if (normalizedType.Contains("KONE"))
            {
                return "KONE";
            }
            if (normalizedType.Contains("ADVANTECH"))
            {
                return "Advantech";
            }
            if (normalizedType.Contains("2N") || normalizedType.Contains("NN"))
            {
                return "2N";
            }
            if (normalizedType.Contains("MYQ"))
            {
                return "MyQ";
            }
            if (normalizedType.Contains("VILLA"))
            {
                return "VillaPro";
            }
            if (normalizedType.Contains("AEOS"))
            {
                return "AEOS";
            }
            if (normalizedType.Contains("TRAFFIC"))
            {
                return "TrafficLight";
            }

            return "Other";
        }
    }
}
