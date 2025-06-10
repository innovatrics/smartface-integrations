using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories
{
    public class UserResolverFactory : IUserResolverFactory
    {
        public const string WATCHLIST_MEMBER_LABEL_TYPE = "WATCHLIST_MEMBER_LABEL";
        public const string AEOS_USER = "AEOS_USER";
        
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public UserResolverFactory(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public IUserResolver Create(string type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            _logger.Information("Creating IUserResolver for type {type}", type);

            var normalizedType = NormalizeType(type);

            switch (normalizedType)
            {
                default:
                    throw new NotImplementedException($"{nameof(IUserResolver)} of type {type} not supported");

                case WATCHLIST_MEMBER_LABEL_TYPE:
                    return new WatchlistMemberLabelUserResolver(_logger, _configuration, _httpClientFactory, type);

                case AEOS_USER:
                    return new AeosUserResolver(_logger, _configuration, _httpClientFactory, type);
            }
        }

        public string NormalizeType(string type)
        {
            var normalizedType = type   
                    .ReplaceAll(new string[] { "-", " ", "." }, new string[] { "_", "_", "_" })
                    .ToUpper();

            if (normalizedType.StartsWith("LABEL_"))
            {
                normalizedType = WATCHLIST_MEMBER_LABEL_TYPE;
            }

            if (normalizedType.StartsWith(WATCHLIST_MEMBER_LABEL_TYPE))
            {
                normalizedType = WATCHLIST_MEMBER_LABEL_TYPE;
            }

            _logger.Information("Normalized type {type} to {normalizedType}", type, normalizedType);

            return normalizedType;
        }
    }
}