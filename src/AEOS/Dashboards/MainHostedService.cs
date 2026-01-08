using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public class MainHostedService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IDataOrchestrator bridge;

        private int SyncPeriodMs = new int();
        private DateTime lastSync;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            IDataOrchestrator bridge
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            SyncPeriodMs = configuration.GetValue<int>("AeosDashboards:Aeos:RefreshPeriodMs");
            if(SyncPeriodMs == 0)
            {
                throw new InvalidOperationException($"The RefreshPeriodMs needs to be greater than 0. Current value: {SyncPeriodMs}");
            }
            lastSync = DateTime.UtcNow;

        }
         
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {

            while(!cancellationToken.IsCancellationRequested)
            {
                
                try
                {
                    await this.bridge.GetLockersData();
                    //await this.bridge.Synchronize();
                    this.logger.Debug("Waiting for {SyncPeriodMs}ms for another refresh.", SyncPeriodMs);    
                    await Task.Delay(SyncPeriodMs,cancellationToken);
                }

                catch (OperationCanceledException)
                {
                    this.logger.Information($"The Service is being shut.");
                }

                catch (Exception e)
                {
                    this.logger.Error(e,"The sync tool failed unexpectedly.");
                }

            }           
            
        }

    }
}