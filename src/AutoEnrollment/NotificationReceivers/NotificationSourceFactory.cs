using System;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.AutoEnrollment.Service;

namespace SmartFace.AutoEnrollment.NotificationReceivers
{
    public class NotificationSourceFactory : INotificationSourceFactory
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly OAuthService _oAuthService;

        public NotificationSourceFactory(ILogger logger, IConfiguration configuration, OAuthService oAuthService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));
        }

        public INotificationSource Create(string notificationSourceType)
        {
            if (notificationSourceType == null)
            {
                throw new ArgumentNullException(nameof(notificationSourceType));
            }

            _logger.Information("Creating INotificationSource for type {Type}", notificationSourceType);

            if (!Enum.TryParse(typeof(NotificationSourceType), notificationSourceType, true, out var result))
            {
                throw new NotImplementedException($"{nameof(INotificationSource)} of type {notificationSourceType} not supported");
            }

            return result switch
            {
                NotificationSourceType.GraphQL => new GraphQlNotificationSource(_logger, _configuration, _oAuthService),
                NotificationSourceType.gRPC => new GrpcNotificationSource(_logger, _configuration),
                _ => throw new NotImplementedException(
                    $"{nameof(INotificationSource)} of type {notificationSourceType} not supported")
            };
        }
    }

    public enum NotificationSourceType
    {
        GraphQL,
        gRPC
    }
}