using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Readers;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Services;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly GrpcReaderFactory _grpcReaderFactory;
        private readonly AccessControlConnectorService _accessControlConnectorService;
        private GrpcNotificationReader _grpcNotificationReader;
        private System.Timers.Timer _accessControllerPingTimer;
        private System.Timers.Timer _keepAlivePingTimer;
        private DateTime _lastGrpcPing;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            GrpcReaderFactory grpcReaderFactory,
            AccessControlConnectorService accessControlConnectorService
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _grpcReaderFactory = grpcReaderFactory ?? throw new ArgumentNullException(nameof(grpcReaderFactory));
            _accessControlConnectorService = accessControlConnectorService ?? throw new ArgumentNullException(nameof(accessControlConnectorService));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is starting");

            _accessControlConnectorService.Start();

            StartReceivingGrpcNotifications();

            StartPingTimer();

            StartKeepAliveTimer();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is stopping");

            await StopReceivingGrpcNotificationsAsync();

            await _accessControlConnectorService.StopAsync();

            _accessControllerPingTimer?.Stop();
            _accessControllerPingTimer?.Dispose();
            _keepAlivePingTimer?.Stop();
            _keepAlivePingTimer?.Dispose();
        }

        private GrpcNotificationReader CreateGrpcReader()
        {
            var grpcHost = _configuration.GetValue<string>("AccessController:Host");
            var grpcPort = _configuration.GetValue<int>("AccessController:Port");

            _logger.Information("gRPC configured to host={host}, port={port}", grpcHost, grpcPort);

            return _grpcReaderFactory.Create(grpcHost, grpcPort);
        }

        private void StartReceivingGrpcNotifications()
        {
            _logger.Information("Start receiving gRPC notifications");

            _grpcNotificationReader = CreateGrpcReader();

            _grpcNotificationReader.OnGrpcGrantedNotification += OnGrpcGrantedNotification;

            _grpcNotificationReader.OnGrpcDeniedNotification += async (DeniedNotification notification) =>
            {
                _logger.Information("Processing 'DENIED' notification {@notification}", new
                {
                    notification.GrpcSentAt,
                    notification.StreamId
                });
            };

            _grpcNotificationReader.OnGrpcBlockedNotification += async (BlockedNotification notification) =>
            {
                _logger.Information("Processing 'BLOCKED' notification {@notification}", new
                {
                    notification.WatchlistMemberDisplayName,
                    notification.WatchlistMemberId,
                    notification.GrpcSentAt,
                    notification.StreamId
                });
            };

            _grpcNotificationReader.OnGrpcPing += OnGrpcPing;

            _grpcNotificationReader.StartReceiving();
        }

        private async Task StopReceivingGrpcNotificationsAsync()
        {
            _grpcNotificationReader.OnGrpcPing -= OnGrpcPing;
            _grpcNotificationReader.OnGrpcGrantedNotification -= OnGrpcGrantedNotification;
            await _grpcNotificationReader.DisposeAsync();
        }

        private Task OnGrpcPing(DateTime sentAt)
        {
            _logger.Debug("gRPC ping received");
            _lastGrpcPing = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        private Task OnGrpcGrantedNotification(GrantedNotification notification)
        {
            _logger.Information("Processing 'GRANTED' notification {@notification}", new
            {
                notification.WatchlistMemberDisplayName,
                notification.WatchlistMemberId,
                notification.GrpcSentAt,
                notification.StreamId
            });

            _logger.Debug("Notification details {@notification}", notification);

            _accessControlConnectorService.ProcessNotification(notification);

            return Task.CompletedTask;
        }

        private void StartPingTimer()
        {
            _lastGrpcPing = DateTime.UtcNow;
            _accessControllerPingTimer = new System.Timers.Timer();

            _accessControllerPingTimer.Interval = 5000;
            _accessControllerPingTimer.Elapsed += async (object sender, System.Timers.ElapsedEventArgs e) =>
            {
                var timeDiff = DateTime.UtcNow - _lastGrpcPing;

                _logger.Debug("Timer ping check: {@ms} ms", timeDiff.TotalMilliseconds);

                if (timeDiff.TotalSeconds > 15)
                {
                    _logger.Warning("gRPC ping not received, last {@ses} sec ago", timeDiff.TotalSeconds);
                }

                if (timeDiff.TotalSeconds > 60)
                {
                    _logger.Error("gRPC ping timeout reached");
                    _logger.Information("gRPC restarting");

                    _accessControllerPingTimer.Stop();

                    await StopReceivingGrpcNotificationsAsync();
                    StartReceivingGrpcNotifications();

                    _accessControllerPingTimer.Start();

                    _logger.Information("gRPC restarted");
                }
            };

            _accessControllerPingTimer.Start();
        }

        private void StartKeepAliveTimer()
        {
            var keepAliveEnabled = _configuration.GetValue<bool>("KeepAlive:Enabled", true);
            var keepAliveInterval = _configuration.GetValue<int>("KeepAlive:Interval", 3600);

            _logger.Information("KeepAlive configured enabled={enabled}, interval={interval}", keepAliveEnabled, keepAliveInterval);

            if (keepAliveEnabled)
            {
                _keepAlivePingTimer = new System.Timers.Timer();

                _keepAlivePingTimer.Interval = keepAliveInterval * 1000;
                _keepAlivePingTimer.Elapsed += async (object sender, System.Timers.ElapsedEventArgs e) =>
                {
                    _logger.Information("KeepAlive interval elapsed, process ping");

                    await _accessControlConnectorService.SendKeepAliveSignalAsync();
                };

                _keepAlivePingTimer.Start();
            }
        }
    }
}