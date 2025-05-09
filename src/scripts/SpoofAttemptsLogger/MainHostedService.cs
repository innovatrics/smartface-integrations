using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Readers;
using System.IO;

namespace Innovatrics.SmartFace.Integrations.SpoofAttemptsLogger
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly GrpcReaderFactory grpcReaderFactory;
        private GrpcNotificationReader grpcNotificationReader;
        private System.Timers.Timer accessControllerPingTimer;
        private System.Timers.Timer keepAlivePingTimer;
        private DateTime lastGrpcPing;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            GrpcReaderFactory grpcReaderFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.grpcReaderFactory = grpcReaderFactory ?? throw new ArgumentNullException(nameof(grpcReaderFactory));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is starting");

            this.logger.Information("Start receiving gRPC notifications");

            this.startReceivingGrpcNotifications();

            this.startPingTimer();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is stopping");

            await this.stopReceivingGrpcNotificationsAsync();

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

            grpcNotificationReader.OnGrpcGrantedNotification += async (GrantedNotification notification) =>
            {
                this.logger.Information("Processing 'GRANTED' notification {@notification}", new
                {
                    notification.WatchlistMemberDisplayName,
                    notification.WatchlistMemberId,
                    notification.GrpcSentAt,
                    notification.StreamId
                });
            };

            grpcNotificationReader.OnGrpcDeniedNotification += async (DeniedNotification notification) =>
            {
                this.logger.Information("Processing 'DENIED' notification {@notification}", new
                {
                    notification.GrpcSentAt,
                    notification.StreamId
                });
            };

            grpcNotificationReader.OnGrpcBlockedNotification += async (BlockedNotification notification) =>
            {
                this.logger.Information("Processing 'BLOCKED' notification {@notification}", new
                {
                    notification.WatchlistMemberDisplayName,
                    notification.WatchlistMemberId,
                    notification.GrpcSentAt,
                    notification.StreamId
                });

                await this.saveBlockedAttemptAsync(notification);
            };

            grpcNotificationReader.OnGrpcPing += OnGrpcPing;

            grpcNotificationReader.StartReceiving();
        }

        private async Task stopReceivingGrpcNotificationsAsync()
        {
            this.grpcNotificationReader.OnGrpcPing -= OnGrpcPing;
            await this.grpcNotificationReader.DisposeAsync();
        }

        private Task OnGrpcPing(DateTime sentAt)
        {
            this.logger.Debug("gRPC ping received");
            this.lastGrpcPing = DateTime.UtcNow;
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

        private async Task saveBlockedAttemptAsync(BlockedNotification notification)
        {
            var targetDirPath = Path.Combine("./Output/Blocked/", $"{notification.GrpcSentAt:yyyy-MM-dd}");

            if (!Directory.Exists(targetDirPath))
            {
                Directory.CreateDirectory(targetDirPath);
            }

            if (notification.CropImage?.Length > 0)
            {
                var cropFileName = $"{notification.GrpcSentAt:HH-mm-ss}-{notification.WatchlistMemberId}-crop.jpeg";

                await File.WriteAllBytesAsync(Path.Combine(targetDirPath, cropFileName), notification.CropImage);
            }


        }
    }
}