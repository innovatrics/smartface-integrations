using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Factories
{
    public class NotificationSourceFactory : INotificationSourceFactory
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IOAuthService oauthService;

        public NotificationSourceFactory(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IOAuthService oauthService
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
        }

        public INotificationSource Create(string notificationSourceType)
        {
            if (notificationSourceType == null)
            {
                throw new ArgumentNullException(nameof(notificationSourceType));
            }

            this.logger.Information("Creating INotificationSource for type {type}", notificationSourceType);

            // Parse the string into an enum, case insensitive
            if (!Enum.TryParse(typeof(NotificationSourceType), notificationSourceType, true, out var result))
            {
                throw new NotImplementedException($"{nameof(INotificationSource)} of type {notificationSourceType} not supported");
            }

            switch (result)
            {
                default:
                    throw new NotImplementedException($"{nameof(INotificationSource)} of type {notificationSourceType} not supported");

                case NotificationSourceType.GraphQL:
                    return new GraphQlNotificationSource(this.logger, this.configuration, this.oauthService);

                case NotificationSourceType.gRPC:
                    return new GrpcNotificationSource(this.logger, this.configuration, this.httpClientFactory);
            }
        }
    }

    public enum NotificationSourceType
    {
        GraphQL,
        gRPC
    }
}