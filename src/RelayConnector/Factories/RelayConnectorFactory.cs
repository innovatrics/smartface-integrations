using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.RelayConnector.Connectors;
using Innovatrics.SmartFace.Integrations.RelayConnector.Connectors.InnerRange;

namespace Innovatrics.SmartFace.Integrations.RelayConnector.Factories
{
    public class RelayConnectorFactory : IRelayConnectorFactory
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public RelayConnectorFactory(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public IRelayConnector Create(string type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            this.logger.Information("Creating IRelayConnector for type {type}", type);

            type = type
                    .ReplaceAll(new string[] { "-", " ", "." }, new string[] { "_", "_", "_" })
                    .ToUpper();

            switch (type)
            {
                default:
                    throw new NotImplementedException($"Relay of type {type} not supported");

                case "ADVANTECH_WISE_4000":
                    return new AdvantechWISE400Connector(this.logger, this.configuration, this.httpClientFactory);
                
                case "INNERRANGE_INTEGRITY_22":
                    return new Integrity22Connector(this.logger, this.configuration, this.httpClientFactory);

                case "TRAFFICLIGHT":
                    return new TrafficLightConnector(this.logger, this.configuration, this.httpClientFactory);
            }
        }
    }
}