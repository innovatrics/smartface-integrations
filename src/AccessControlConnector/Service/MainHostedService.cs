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
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly GrpcReaderFactory grpcReaderFactory;
        private readonly AccessControlConnectorService _accessControlConnectorService;
        private GrpcNotificationReader grpcNotificationReader;
        private System.Timers.Timer accessControllerPingTimer;
        private System.Timers.Timer keepAlivePingTimer;
        private DateTime lastGrpcPing;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            GrpcReaderFactory grpcReaderFactory,
            AccessControlConnectorService accessControlConnectorService
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.grpcReaderFactory = grpcReaderFactory ?? throw new ArgumentNullException(nameof(grpcReaderFactory));
            _accessControlConnectorService = accessControlConnectorService ?? throw new ArgumentNullException(nameof(accessControlConnectorService));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is starting");

            this.logger.Information("Start receiving gRPC notifications");

            _accessControlConnectorService.Start();

            this.startReceivingGrpcNotifications();

            this.startPingTimer();

            this.startKeepAliveTimer();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is stopping");

            await this.stopReceivingGrpcNotificationsAsync();

            await _accessControlConnectorService.StopAsync();

            this.accessControllerPingTimer?.Stop();
            this.accessControllerPingTimer?.Dispose();
            this.keepAlivePingTimer?.Stop();
            this.keepAlivePingTimer?.Dispose();
        }

        private GrpcNotificationReader CreateGrpcReader()
        {
            var grpcHost = this.configuration.GetValue<string>("AccessController:Host");
            var grpcPort = this.configuration.GetValue<int>("AccessController:Port");

            this.logger.Information("gRPC configured to host={host}, port={port}", grpcHost, grpcPort);

            return this.grpcReaderFactory.Create(grpcHost, grpcPort);
        }

        private void startReceivingGrpcNotifications()
        {
            this.logger.Information("Start receiving gRPC notifications");

            grpcNotificationReader = this.CreateGrpcReader();

            grpcNotificationReader.OnGrpcGrantedNotification += OnGrpcGrantedNotification;

            grpcNotificationReader.OnGrpcDeniedNotification += async (DeniedNotification notification) =>
            {
                this.logger.Information("Processing 'DENIED' notification {@notification}", new
                {
                    FaceDetectedAt = notification.FaceDetectedAt,
                    StreamId = notification.StreamId
                });
            };

            grpcNotificationReader.OnGrpcBlockedNotification += async (BlockedNotification notification) =>
            {
                this.logger.Information("Processing 'BLOCKED' notification {@notification}", new
                {
                    WatchlistMemberFullName = notification.WatchlistMemberFullName,
                    WatchlistMemberId = notification.WatchlistMemberId,
                    FaceDetectedAt = notification.FaceDetectedAt,
                    StreamId = notification.StreamId
                });
            };

            grpcNotificationReader.OnGrpcPing += OnGrpcPing;

            grpcNotificationReader.StartReceiving();
        }

        private async Task stopReceivingGrpcNotificationsAsync()
        {
            this.grpcNotificationReader.OnGrpcPing -= OnGrpcPing;
            this.grpcNotificationReader.OnGrpcGrantedNotification -= OnGrpcGrantedNotification;
            await this.grpcNotificationReader.DisposeAsync();
        }

        private Task OnGrpcPing(DateTime sentAt)
        {
            this.logger.Debug("gRPC ping received");
            this.lastGrpcPing = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        private Task OnGrpcGrantedNotification(GrantedNotification notification)
        {
            this.logger.Information("Processing 'GRANTED' notification {@notification}", new
            {
                WatchlistMemberFullName = notification.WatchlistMemberFullName,
                WatchlistMemberId = notification.WatchlistMemberId,
                FaceDetectedAt = notification.FaceDetectedAt,
                StreamId = notification.StreamId
            });

            this.logger.Debug("Notification details {@notification}", notification);

            this._accessControlConnectorService.ProcessNotification(notification);

            return Task.CompletedTask;
        }

        private void startPingTimer()
        {
            this.lastGrpcPing = DateTime.UtcNow;
            accessControllerPingTimer = new System.Timers.Timer();

            accessControllerPingTimer.Interval = 5000;
            accessControllerPingTimer.Elapsed += async (object sender, System.Timers.ElapsedEventArgs e) =>
            {
                var timeDiff = DateTime.UtcNow - lastGrpcPing;

                this.logger.Debug("Timer ping check: {@ms} ms", timeDiff.TotalMilliseconds);

                if (timeDiff.TotalSeconds > 15)
                {
                    this.logger.Warning("gRPC ping not received, last {@ses} sec ago", timeDiff.TotalSeconds);
                }

                if (timeDiff.TotalSeconds > 60)
                {
                    this.logger.Error("gRPC ping timeout reached");
                    this.logger.Information("gRPC restarting");

                    accessControllerPingTimer.Stop();

                    await this.stopReceivingGrpcNotificationsAsync();
                    this.startReceivingGrpcNotifications();

                    accessControllerPingTimer.Start();

                    this.logger.Information("gRPC restarted");
                }
            };

            accessControllerPingTimer.Start();
        }

        private void startKeepAliveTimer()
        {
            var keepAliveEnabled = this.configuration.GetValue<bool>("KeepAlive:Enabled", true);
            var keepAliveInterval = this.configuration.GetValue<int>("KeepAlive:Interval", 3600);

            this.logger.Information("KeepAlive configured enabled={enabled}, interval={interval}", keepAliveEnabled, keepAliveInterval);

            if (keepAliveEnabled)
            {
                keepAlivePingTimer = new System.Timers.Timer();

                keepAlivePingTimer.Interval = keepAliveInterval * 1000;
                keepAlivePingTimer.Elapsed += async (object sender, System.Timers.ElapsedEventArgs e) =>
                {
                    this.logger.Information("KeepAlive interval elapsed, process ping");

                    await this._accessControlConnectorService.SendKeepAliveSignalAsync();
                };

                keepAlivePingTimer.Start();
            }
        }
    }
}