using System;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.AutoEnrollment.Service;

namespace SmartFace.AutoEnrollment.NotificationReceivers
{
    public class NotificationSourceFactory(ILogger logger, IConfiguration configuration, OAuthService oAuthService) : INotificationSourceFactory
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        private readonly OAuthService _oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));

        public INotificationSource Create(string notificationSourceType)
        {
            ArgumentNullException.ThrowIfNull(notificationSourceType);

            _logger.Information("Creating INotificationSource for type {Type}", notificationSourceType);

            if (!Enum.TryParse(typeof(NotificationSourceType), notificationSourceType, true, out var result))
            {
                throw new NotImplementedException($"{nameof(INotificationSource)} of type {notificationSourceType} not supported");
            }

            switch (result)
            {
                case NotificationSourceType.GraphQL:
                    return new GraphQlNotificationSource(_logger, _configuration, _oAuthService);

                case NotificationSourceType.gRPC:
                    throw new NotImplementedException("gRPC notification source not yet implemented");
                    
                default:
                    throw new NotImplementedException(
                        $"{nameof(INotificationSource)} of type {notificationSourceType} not supported");
            }
        }
    }

    public enum NotificationSourceType
    {
        GraphQL,
        gRPC
    }
}