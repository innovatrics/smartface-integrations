using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Readers;
using Innovatrics.SmartFace.Integrations.MyQConnector.Services;

namespace Innovatrics.SmartFace.Integrations.MyQConnector
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly GrpcReaderFactory grpcReaderFactory;
        private readonly IBridgeService bridge;
        private GrpcNotificationReader grpcNotificationReader;
        private System.Timers.Timer accessControllerPingTimer;
        private DateTime lastGrpcPing;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            GrpcReaderFactory grpcReaderFactory,
            IBridgeService bridge
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.grpcReaderFactory = grpcReaderFactory ?? throw new ArgumentNullException(nameof(grpcReaderFactory));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is starting");

            this.logger.Information("Start receiving gRPC notifications");

            this.StartReceivingGrpcNotifications();

            this.StartPingTimer();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is stopping");

            await this.StopReceivingGrpcNotificationsAsync();

            this.accessControllerPingTimer?.Stop();
            this.accessControllerPingTimer?.Dispose();
        }

        private GrpcNotificationReader CreateGrpcReader()
        {
            var grpcHost = this.configuration.GetValue<string>("AccessController:Host");
            var grpcPort = this.configuration.GetValue<int>("AccessController:Port");

            this.logger.Information("gRPC configured to host={host}, port={port}", grpcHost, grpcPort);

            return this.grpcReaderFactory.Create(grpcHost, grpcPort);
        }

        private void StartReceivingGrpcNotifications()
        {
            this.logger.Information("Start receiving gRPC notifications");

            grpcNotificationReader = this.CreateGrpcReader();

            grpcNotificationReader.OnGrpcFaceGrantedNotification += OnGrpcGrantedNotification;
            grpcNotificationReader.OnGrpcPing += OnGrpcPing;

            grpcNotificationReader.StartReceiving();
        }

        private async Task StopReceivingGrpcNotificationsAsync()
        {
            this.grpcNotificationReader.OnGrpcPing -= OnGrpcPing;
            this.grpcNotificationReader.OnGrpcFaceGrantedNotification -= OnGrpcGrantedNotification;
            await this.grpcNotificationReader.DisposeAsync();
        }

        private Task OnGrpcPing(DateTime sentAt)
        {
            this.logger.Debug("gRPC ping received");
            this.lastGrpcPing = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        private async Task OnGrpcGrantedNotification(FaceGrantedNotification notification)
        {
            this.logger.Information("Processing 'GRANTED' notification {@notification}", new
            {
                WatchlistMemberFullName = notification.WatchlistMemberFullName,
                WatchlistMemberId = notification.WatchlistMemberId,
                FaceDetectedAt = notification.FaceDetectedAt,
                StreamId = notification.StreamId
            });

            this.logger.Debug("Notification details {@notification}", notification);

            await this.bridge.ProcessFaceGrantedNotificationAsync(notification);
        }

        private void StartPingTimer()
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

                    await this.StopReceivingGrpcNotificationsAsync();
                    this.StartReceivingGrpcNotifications();

                    accessControllerPingTimer.Start();

                    this.logger.Information("gRPC restarted");
                }
            };

            accessControllerPingTimer.Start();
        }
    }
}