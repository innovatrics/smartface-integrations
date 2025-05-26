using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using Innovatrics.SmartFace.Integrations.AEOS.SmartFaceClients;
using System.IO;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public class DataOrchestrator : IDataOrchestrator
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IAeosDataAdapter aeosDataAdapter;
        
        public DataOrchestrator(
            ILogger logger,
            IConfiguration configuration,
            IAeosDataAdapter aeosDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.aeosDataAdapter = aeosDataAdapter ?? throw new ArgumentNullException(nameof(aeosDataAdapter));

        }

        public async Task GetLockersData()
        {
            this.logger.Information("Retrieving lockers data from AEOS.");

            var lockers = await this.aeosDataAdapter.GetLockers();

            foreach (var locker in lockers)
            {
                this.logger.Information(locker.ToString());
                this.logger.Information($"{locker.Id}, {locker.Name}, {locker.LastUsed}, {locker.AssignedTo}");
            }

            // process lockers data here
        }

    
}
}