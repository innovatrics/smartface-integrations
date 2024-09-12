using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Readers;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Factories;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly GrpcReaderFactory grpcReaderFactory;
        private readonly IAutoEnrollmentService bridge;
        private readonly INotificationSourceFactory notificationSourceFactory;
        private INotificationSource notificationSource;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            GrpcReaderFactory grpcReaderFactory,
            INotificationSourceFactory notificationSourceFactory,
            IAutoEnrollmentService bridge
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.grpcReaderFactory = grpcReaderFactory ?? throw new ArgumentNullException(nameof(grpcReaderFactory));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            this.notificationSourceFactory = notificationSourceFactory ?? throw new ArgumentNullException(nameof(notificationSourceFactory));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is starting");

            var notificationSourceType = this.configuration.GetValue<string>("NotificationSource", "GraphQL");

            this.notificationSource = this.notificationSourceFactory.Create(notificationSourceType);

            this.notificationSource.OnNotification += OnNotification;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is stopping");

            return this.notificationSource.StopAsync();
        }

        private async Task OnNotification(object notification)
        {
            this.logger.Information("Processing OnNotification {@notification}", new
            {
                // WatchlistMemberFullName = notification.WatchlistMemberFullName,
                // WatchlistMemberId = notification.WatchlistMemberId,
                // FaceDetectedAt = notification.FaceDetectedAt,
                // StreamId = notification.StreamId
            });

            await this.bridge.ProcessGrantedNotificationAsync(notification);
        }
    }
}