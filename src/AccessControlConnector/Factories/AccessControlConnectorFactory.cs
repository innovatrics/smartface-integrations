using System;
using System.Net.Http;
using AccessControlConnector.Connectors;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.AXIS;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.NN;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using AccessControlConnector.Connectors.Kone;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories
{
    public class AccessControlConnectorFactory
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public AccessControlConnectorFactory(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public IAccessControlConnector Create(string accessConnectorType)
        {
            if (accessConnectorType == null)
            {
                throw new ArgumentNullException(nameof(accessConnectorType));
            }

            _logger.Information("Creating IAccessControlConnector for type {type}", accessConnectorType);

            accessConnectorType = accessConnectorType.ReplaceAll(["-", " ", "."], ["_", "_", "_"]).ToUpper();

            switch (accessConnectorType)
            {
                case AccessControlConnectorTypes.ADVANTECH_WISE_4000:
                    return new AdvantechWISE4000Connector(_logger, _configuration, _httpClientFactory);

                case AccessControlConnectorTypes.INNERRANGE_INTEGRITI_22:
                    return new Integriti22Connector(_logger, _configuration, _httpClientFactory);

                case AccessControlConnectorTypes.TRAFFICLIGHT:
                    return new TrafficLightConnector(_logger, _configuration, _httpClientFactory);

                case AccessControlConnectorTypes.AXIS_A1001:
                    return new A1001Connector(_logger, _configuration, _httpClientFactory);

                case AccessControlConnectorTypes.AXIS_SIRENE:
                    return new SireneAndLightConnector(_logger, _configuration, _httpClientFactory);

                case AccessControlConnectorTypes.AXIS_IO_PORT:
                    return new IOPortConnector(_logger, _httpClientFactory);

                case AccessControlConnectorTypes.NN_IP_INTERCOM:
                    return new IpIntercomConnector(_logger, _httpClientFactory);

                case AccessControlConnectorTypes.MYQ_CONNECTOR:
                    return new MyQConnector(_logger, _configuration, _httpClientFactory);

                case AccessControlConnectorTypes.SHARRY_CHECK_IN_CONNECTOR:
                    return new SharryCheckInConnector(_logger, _configuration, _httpClientFactory);

                case AccessControlConnectorTypes.VILLA_PRO_CONNECTOR:
                    return new VillaProConnector(_logger, _configuration, _httpClientFactory);

                case AccessControlConnectorTypes.AEOS_CONNECTOR:
                    return new AeosConnector(_logger, _configuration, _httpClientFactory);

                case AccessControlConnectorTypes.KONE_CONNECTOR:
                    return KoneConnectorFactory.Create(_logger, _configuration);

                case AccessControlConnectorTypes.DUMMY_CONNECTOR:
                    return new DummyConnector();

                default:
                    throw new NotImplementedException($"Access Connector of type {accessConnectorType} not supported");
            }
        }
    }
}