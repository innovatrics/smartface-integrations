using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IDataOrchestrator bridge;
        private System.Timers.Timer timerPing;

        private DateTime timerStartSync = new DateTime();

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            IDataOrchestrator bridge
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Debug($"{nameof(MainHostedService)} is starting");

            timerPing = new System.Timers.Timer();
            this.timerStartSync = DateTime.UtcNow;
            this.logger.Information($"Initialization Synchronization at {DateTime.UtcNow}");
            return this.bridge.Synchronize();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Debug($"{nameof(MainHostedService)} is stopping");
            var timeDiff = DateTime.UtcNow - this.timerStartSync;
            this.logger.Information($"Finishing Synchronization at {DateTime.UtcNow}");
            this.logger.Information("Synchronization took: {@ms} s", timeDiff.TotalSeconds);
            timerPing.Stop();
        }

    }
}