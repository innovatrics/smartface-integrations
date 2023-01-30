using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public class SmartFaceDataAdapter : ISmartFaceDataAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public SmartFaceDataAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.logger.Information("SmartFaceDataAdapter Initiated");
        }

        //public async Task OpenAsync(string checkpoint_id, string ticket_id, int chip_id = 12)
        public async Task OpenAsync()
        {
            this.logger.Information("SmartFaceDataAdapter");

        }

         public async Task getEmployees()
        {
            this.logger.Information("Receiving Employees");
        }

        public async Task createEmployees()
        {
            this.logger.Information("Creating Employees");
        }

        public async Task updateEmployees()
        {
            this.logger.Information("Updating Employees");
        }

        public async Task removeEmployees()
        {
            this.logger.Information("Removing Employees");
        }
    }
}