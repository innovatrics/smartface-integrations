using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.MyQConnectorNamespace
{
    public class ConfigurationAdapter : IConfigurationAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private string PrinterConnection;

        public ConfigurationAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            this.logger.Debug("ConfigurationAdapter Initiated");

            PrinterConnection = configuration.GetValue<string>("MyConfiguration:PrinterServer") ?? throw new InvalidOperationException("The PrinterServer IP + PORT are not available");
            
            if (PrinterConnection == null)
            {
                throw new InvalidOperationException("PrinterConnection was not set.");
            }
            else
            {
                this.logger.Debug("PrinterConnection: " + PrinterConnection);
            }


        }

    }
}
