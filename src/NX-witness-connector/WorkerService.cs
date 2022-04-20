using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

using Innovatrics.SmartFace.Integrations.Shared.ZeroMQ;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    internal class WorkerService : IHostedService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly ZeroMqNotificationReader zeroMqNotificationReader;

        public WorkerService(
            ILogger logger,
            IConfiguration configuration
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            // this.zeroMqNotificationReader = new ZeroMqNotificationReader();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(WorkerService)} is starting");

            this.logger.Information("Start receiving ZeroMQ notifications");

            // this.startReceivingGrpcNotifications();

            // this.lastGrpcPing = DateTime.UtcNow;
            // timerPing = new System.Timers.Timer();

            // timerPing.Interval = 5000;
            // timerPing.Elapsed += async (object sender, System.Timers.ElapsedEventArgs e) =>
            // {
            //     var timeDiff = DateTime.UtcNow - lastGrpcPing;

            //     this.logger.Debug("Timer ping check: {@ms} ms", timeDiff.TotalMilliseconds);

            //     if (timeDiff.TotalSeconds > 15)
            //     {
            //         this.logger.Warning("gRPC ping not received, last {@ses} sec ago", timeDiff.TotalSeconds);
            //     }

            //     if (timeDiff.TotalSeconds > 60)
            //     {
            //         this.logger.Error("gRPC ping timeout reached");
            //         this.logger.Information("gRPC restarting");

            //         timerPing.Stop();

            //         await this.stopReceivingGrpcNotificationsAsync();
            //         this.startReceivingGrpcNotifications();

            //         timerPing.Start();

            //         this.logger.Information("gRPC restarted");
            //     }
            // };

            // timerPing.Start();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(WorkerService)} is stopping");

            // await this.stopReceivingGrpcNotificationsAsync();

            // this.timerPing?.Stop();
            // this.timerPing?.Dispose();
        }
    }
}