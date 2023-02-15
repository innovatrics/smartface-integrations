using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AEpuConnector.Connectors;

namespace Innovatrics.SmartFace.Integrations.AEpuConnector.Factories
{
    public class AEpuConnectorFactory : IAEpuConnectorFactory
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public AEpuConnectorFactory(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public IAEpuConnector Create(string type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            this.logger.Information("Creating IAEpuConnector for type {type}", type);

            type = type
                    .ReplaceAll(new string[] { "-", " ", "." }, new string[] { "_", "_", "_" })
                    .ToUpper();

            switch (type)
            {
                default:
                    throw new NotImplementedException($"AEpu Connector of type {type} not supported");

                case "AEPU":
                    return new Connectors.AEpuConnector(this.logger, this.configuration, this.httpClientFactory);
            }
        }
    }
}