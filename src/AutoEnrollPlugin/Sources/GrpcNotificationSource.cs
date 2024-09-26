using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Readers;

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
        public event Func<Models.Notification, Task> OnNotification;

        public GrpcNotificationSource(
            ILogger logger,
            IConfiguration configuration
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

            grpcNotificationReader.OnGrpcFaceGrantedNotification += (FaceGrantedNotification Notification) =>
            {
                this.logger.Information("Processing 'GRANTED' notification skipped");
                return Task.CompletedTask;
            };

            grpcNotificationReader.OnGrpcFaceDeniedNotification += (FaceDeniedNotification notification) =>
            {
                this.logger.Information("Processing 'DENIED' notification skipped");

                var notification2 =

                this.OnNotification?.Invoke(new Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models.Notification()
                {
                    StreamId = notification.StreamId,
                    FaceId = notification.FaceId,
                    TrackletId = notification.TrackletId,
                    CropImage = notification.CropImage,
                    ReceivedAt = DateTime.UtcNow
                });
            };

            grpcNotificationReader.OnGrpcFaceBlockedNotification += (FaceBlockedNotification notification) =>
            {
                this.logger.Information("Processing 'BLOCKED' notification skipped");
            };

            grpcNotificationReader.OnGrpcPing += OnGrpcPing;

            grpcNotificationReader.StartReceiving();
        }

        private async Task stopReceivingGrpcNotificationsAsync()
        {
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
    }
}