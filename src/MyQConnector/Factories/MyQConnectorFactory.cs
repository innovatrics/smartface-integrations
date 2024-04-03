using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Connectors;

namespace Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Factories
{
    public class MyQConnectorFactory : IMyQConnectorFactory
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public MyQConnectorFactory(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public MyQConnector Create(string type)
        {https://msdn.microsoft.com/query/roslyn.query?appId%3Droslyn%26k%3Dk%28CS0118%29
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            this.logger.Information("Creating IMyQConnector for type {type}", type);

            type = type
                    .ReplaceAll(new string[] { "-", " ", "." }, new string[] { "_", "_", "_" })
                    .ToUpper();

            switch (type)
            {
                default:
                    throw new NotImplementedException($"MyQConnector of type {type} not supported");

                case "MyQ":
                    return new Connectors.MyQConnector(this.logger, this.configuration, this.httpClientFactory);
            }
        }
    }
}