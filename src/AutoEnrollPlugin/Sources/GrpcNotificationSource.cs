using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Readers;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models
{
    
}

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources
{
    public class GrpcNotificationSource : INotificationSource
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly GrpcReaderFactory grpcReaderFactory;
        private GrpcNotificationReader grpcNotificationReader;
        private System.Timers.Timer accessControllerPingTimer;
        private DateTime lastGrpcPing;
        public event Func<Notification22, Task> OnNotification;

        public GrpcNotificationSource(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public Task StartAsync()
        {
            this.logger.Information("Start receiving gRPC notifications");

            this.startReceivingGrpcNotifications();

            this.startPingTimer();

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            this.logger.Information($"Stopping receiving gRPC notifications");

            await this.stopReceivingGrpcNotificationsAsync();

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

        private void startReceivingGrpcNotifications()
        {
            this.logger.Information("Start receiving gRPC notifications");

            grpcNotificationReader = this.CreateGrpcReader();

            grpcNotificationReader.OnGrpcGrantedNotification += OnGrpcGrantedNotification;

            grpcNotificationReader.OnGrpcDeniedNotification += (DeniedNotification notification) =>
            {
                this.logger.Information("Processing 'DENIED' notification {@notification}", new
                {
                    FaceDetectedAt = notification.FaceDetectedAt,
                    StreamId = notification.StreamId
                });
            };

            grpcNotificationReader.OnGrpcBlockedNotification += (BlockedNotification notification) =>
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

            this.OnNotification?.Invoke(new Notification22());

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
    }
}