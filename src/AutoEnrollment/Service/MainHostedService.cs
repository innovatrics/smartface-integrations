using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmartFace.AutoEnrollment.Models;
using SmartFace.AutoEnrollment.NotificationReceivers;

namespace SmartFace.AutoEnrollment.Service
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly QueueProcessingService _queueProcessingService;
        private readonly INotificationSourceFactory _notificationSourceFactory;
        private INotificationSource _notificationSource;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            INotificationSourceFactory notificationSourceFactory,
            QueueProcessingService QueueProcessingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _queueProcessingService = QueueProcessingService ?? throw new ArgumentNullException(nameof(QueueProcessingService));
            _notificationSourceFactory = notificationSourceFactory ?? throw new ArgumentNullException(nameof(notificationSourceFactory));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is starting");

            var notificationSourceType = _configuration.GetValue("Source:Type", "GraphQL");

            _notificationSource = _notificationSourceFactory.Create(notificationSourceType);

            _notificationSource.OnNotification += HandleNotificationAsync;

            await _notificationSource.StartAsync();
            _queueProcessingService.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is stopping");

            await _notificationSource.StopAsync();
            await _queueProcessingService.StopAsync();
        }

        private Task HandleNotificationAsync(Notification notification)
        {
            _logger.Information("Processing HandleNotificationAsync {notification}", new { notification.StreamId, notification.ReceivedAt });

            _queueProcessingService.ProcessNotification(notification);

            return Task.CompletedTask;
        }
    }
}