using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AEOSConnector.Connectors;

namespace Innovatrics.SmartFace.Integrations.AEOSConnector.Factories
{
    public class AEOSConnectorFactory : IAEOSConnectorFactory
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public AEOSConnectorFactory(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public IAEOSConnector Create(string type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            this.logger.Information("Creating IAEOSConnector for type {type}", type);

            type = type
                    .ReplaceAll(new string[] { "-", " ", "." }, new string[] { "_", "_", "_" })
                    .ToUpper();

            switch (type)
            {
                default:
                    throw new NotImplementedException($"AEOS Connector of type {type} not supported");

                case "AEpu":
                    return new AEpuConnector(this.logger, this.configuration, this.httpClientFactory);
            }
        }
    }
}