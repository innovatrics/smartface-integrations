using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class MainHostedService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IDataOrchestrator bridge;
        private readonly IDashboardsDataAdapter dashboardsDataAdapter;

        private int SyncPeriodMs = new int();
        private DateTime lastSync;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            IDataOrchestrator bridge,
            IDashboardsDataAdapter dashboardsDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            this.dashboardsDataAdapter = dashboardsDataAdapter ?? throw new ArgumentNullException(nameof(dashboardsDataAdapter));

            SyncPeriodMs = configuration.GetValue<int>("LockerMailer:RefreshPeriodMs", 300000); // Default to 5 minutes
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
                    // Get email summary assignment changes from Dashboards API
                    var emailSummary = await this.dashboardsDataAdapter.GetEmailSummaryAssignmentChanges();
                    this.logger.Information($"Retrieved {emailSummary.TotalChanges} assignment changes from Dashboards API");
                    
                    // Process the data through the orchestrator
                    //await this.bridge.GetLockersData();
                    await this.bridge.ProcessEmailSummaryAssignmentChanges(emailSummary);
                    this.logger.Debug("Waiting for {SyncPeriodMs}ms for another refresh.", SyncPeriodMs);    
                    await Task.Delay(SyncPeriodMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    this.logger.Information($"The Service is being shut.");
                }
                catch (Exception e)
                {
                    this.logger.Error(e, "The sync tool failed unexpectedly.");
                }
            }           
        }
    }
}