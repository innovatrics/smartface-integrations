using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmartFace.GoogleCalendarsConnector.Models;
using SmartFace.GoogleCalendarsConnector.Service;

namespace SmartFace.GoogleCalendarsConnector.Service
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly QueueProcessingService _queueProcessingService;
        private readonly GraphQlNotificationsService _graphQlNotificationsService;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            GraphQlNotificationsService graphQlNotificationsService,
            QueueProcessingService queueProcessingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _queueProcessingService = queueProcessingService ?? throw new ArgumentNullException(nameof(queueProcessingService));
            _graphQlNotificationsService = graphQlNotificationsService ?? throw new ArgumentNullException(nameof(graphQlNotificationsService));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is starting");

            _graphQlNotificationsService.OnStreamGroupAggregation += HandleNotificationAsync;

            await _graphQlNotificationsService.StartAsync();
            _queueProcessingService.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is stopping");

            await _graphQlNotificationsService.StopAsync();
            await _queueProcessingService.StopAsync();
        }

        private Task HandleNotificationAsync(StreamGroupAggregation notification)
        {
            _logger.Information("Processing HandleNotificationAsync {notification}", new { notification.StreamGroupName, notification.Timestamp });

            _queueProcessingService.ProcessNotification(notification);

            return Task.CompletedTask;
        }
    }
}