using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.DataCollection.Models;

namespace Innovatrics.SmartFace.DataCollection.Services
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly QueueProcessingService _queueProcessingService;
        private IList<GraphQlNotificationService> _notificationSources;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            QueueProcessingService QueueProcessingService
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _queueProcessingService = QueueProcessingService ?? throw new ArgumentNullException(nameof(QueueProcessingService));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is starting");

            var sources = _configuration.GetValue<Source[]>("Sources");

            _notificationSources = new List<GraphQlNotificationService>();

            foreach (var source in sources)
            {
                var notificationSource = new GraphQlNotificationService(
                    _logger,
                    source.Schema,
                    source.Host,
                    source.Port,
                    source.Path
                );

                notificationSource.OnNotification += HandleNotificationAsync;

                notificationSource.Start();

                _notificationSources.Add(notificationSource);
            }

            _queueProcessingService.Start();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is stopping");

            foreach (var notificationSource in _notificationSources)
            {
                notificationSource.Stop();
            }

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