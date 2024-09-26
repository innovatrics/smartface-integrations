using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;
using AutoEnrollPlugin.Sources;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly AutoEnrollmentService _autoEnrollmentService;
        private readonly INotificationSourceFactory _notificationSourceFactory;
        private INotificationSource _notificationSource;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            INotificationSourceFactory notificationSourceFactory,
            AutoEnrollmentService autoEnrollmentService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _autoEnrollmentService = autoEnrollmentService ?? throw new ArgumentNullException(nameof(autoEnrollmentService));
            _notificationSourceFactory = notificationSourceFactory ?? throw new ArgumentNullException(nameof(notificationSourceFactory));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is starting");

            var notificationSourceType = _configuration.GetValue("Source:Type", "GraphQL");

            _notificationSource = _notificationSourceFactory.Create(notificationSourceType);

            _notificationSource.OnNotification += HandleNotificationAsync;

            await _notificationSource.StartAsync();
            _autoEnrollmentService.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is stopping");

            await _notificationSource.StopAsync();
            await _autoEnrollmentService.StopAsync();
        }

        private Task HandleNotificationAsync(Notification notification)
        {
            _logger.Information("Processing HandleNotificationAsync {notification}", new { notification.StreamId, notification.ReceivedAt });

            _autoEnrollmentService.ProcessNotification(notification);

            return Task.CompletedTask;
        }
    }
}