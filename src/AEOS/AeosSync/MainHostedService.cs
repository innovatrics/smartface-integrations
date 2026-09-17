using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public class MainHostedService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IDataOrchestrator bridge;
        private System.Timers.Timer timerPing;

        private DateTime timerStartSync = new DateTime();
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

            SyncPeriodMs = configuration.GetValue<int>("AeosSync:SyncPeriodMs");
            if(SyncPeriodMs == 0)
            {
                throw new InvalidOperationException("The SyncPeriodMs needs to be greater than 0.");
            }
            lastSync = DateTime.UtcNow;

        }
         
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            this.logger.Information("Synchronization runs in polling mode: a full sync is executed every {SyncPeriodMs}ms.", SyncPeriodMs);

            while(!cancellationToken.IsCancellationRequested)
            {

                try
                {
                    var syncStartedAt = DateTime.UtcNow;
                    await this.bridge.Synchronize();
                    this.logger.Information("Sync poll completed in {ElapsedMs}ms. Next poll in {SyncPeriodMs}ms.", (long)(DateTime.UtcNow - syncStartedAt).TotalMilliseconds, SyncPeriodMs);
                }

                catch (OperationCanceledException)
                {
                    this.logger.Information($"The Service is being shut.");
                    break;
                }

                catch (Exception e)
                {
                    this.logger.Error(e,"The sync tool failed unexpectedly. Next sync attempt in {SyncPeriodMs}ms.", SyncPeriodMs);
                }

                try
                {
                    this.logger.Debug("Waiting for {SyncPeriodMs}ms for another sync.", SyncPeriodMs);
                    await Task.Delay(SyncPeriodMs,cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    this.logger.Information($"The Service is being shut.");
                }

            }
            
        }

    }
}