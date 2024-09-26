using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Factories;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly AutoEnrollmentService autoEnrollmentService;
        private readonly INotificationSourceFactory notificationSourceFactory;
        private INotificationSource notificationSource;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            INotificationSourceFactory notificationSourceFactory,
            AutoEnrollmentService autoEnrollmentService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.autoEnrollmentService = autoEnrollmentService ?? throw new ArgumentNullException(nameof(autoEnrollmentService));
            this.notificationSourceFactory = notificationSourceFactory ?? throw new ArgumentNullException(nameof(notificationSourceFactory));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is starting");

            var notificationSourceType = this.configuration.GetValue<string>("Source:Type", "GraphQL");

            this.notificationSource = this.notificationSourceFactory.Create(notificationSourceType);

            this.notificationSource.OnNotification += OnNotification;

            await this.notificationSource.StartAsync();
            this.autoEnrollmentService.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(MainHostedService)} is stopping");

            await this.notificationSource.StopAsync();
            await this.autoEnrollmentService.StopAsync();
        }

        private Task OnNotification(Notification notification)
        {
            this.logger.Information("Processing OnNotification {notification}", new { notification.StreamId, notification.ReceivedAt });

            this.autoEnrollmentService.ProcessNotification(notification);

            return Task.CompletedTask;
        }
    }
}