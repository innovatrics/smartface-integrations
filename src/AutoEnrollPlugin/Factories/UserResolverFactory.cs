using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources.InnerRange;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Resolvers;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Factories
{
    public class UserResolverFactory : IUserResolverFactory
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public UserResolverFactory(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public IUserResolver Create(string type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            this.logger.Information("Creating IUserResolver for type {type}", type);

            type = type
                    .ReplaceAll(new string[] { "-", " ", "." }, new string[] { "_", "_", "_" })
                    .ToUpper();

            switch (type)
            {
                default:
                    throw new NotImplementedException($"IUserResolver of type {type} not supported");

                case "LABEL_ACCESS_CARD_ID":
                    return new WatchlistMemberLabelUserResolver(this.logger, this.configuration, this.httpClientFactory, type);
            }
        }
    }
}