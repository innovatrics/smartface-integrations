using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.AXIS;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.NN;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories
{
    public class AccessControlConnectorFactory : IAccessControlConnectorFactory
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public AccessControlConnectorFactory(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public IAccessControlConnector Create(string type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            this.logger.Information("Creating IAccessControlConnector for type {type}", type);

            type = type
                    .ReplaceAll(new string[] { "-", " ", "." }, new string[] { "_", "_", "_" })
                    .ToUpper();

            switch (type)
            {
                default:
                    throw new NotImplementedException($"AccessControl of type {type} not supported");

                case AccessControlConnectorTypes.ADVANTECH_WISE_4000:
                    return new AdvantechWISE4000Connector(this.logger, this.configuration, this.httpClientFactory);

                case AccessControlConnectorTypes.INNERRANGE_INTEGRITY_22:
                    return new Integrity22Connector(this.logger, this.configuration, this.httpClientFactory);

                case AccessControlConnectorTypes.TRAFFICLIGHT:
                    return new TrafficLightConnector(this.logger, this.configuration, this.httpClientFactory);

                case AccessControlConnectorTypes.AXIS_A1001:
                    return new A1001Connector(this.logger, this.configuration, this.httpClientFactory);

                case AccessControlConnectorTypes.AXIS_SIRENE:
                    return new SireneAndLightConnector(this.logger, this.configuration, this.httpClientFactory);

                case AccessControlConnectorTypes.AXIS_IO_PORT:
                    return new IOPortConnector(this.logger, this.httpClientFactory);
                
                case AccessControlConnectorTypes.NN_IP_INTERCOM:
                    return new IpIntercomConnector(this.logger, this.httpClientFactory);
            }
        }
    }
}