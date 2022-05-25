using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

using Innovatrics.SmartFace.Integrations.Shared.ZeroMQ;

namespace Innovatrics.SmartFace.Integrations.NotificationsReceiver
{
    public class WorkerService : IHostedService
    {
        private const string ZERO_MQ_DEFAULT_HOST = "localhost";
        private const int ZERO_MQ_DEFAULT_PORT = 2406;

        private readonly ILogger logger;
        private readonly IConfiguration configuration;

        public WorkerService(
            ILogger logger,
            IConfiguration configuration
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(WorkerService)} is starting");

            this.startReceivingZeroMqotifications(cancellationToken);

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(WorkerService)} is stopping");

            this.stopReceivingZeroMqotifications();
        }

        private void startReceivingZeroMqotifications(CancellationToken cancellationToken)
        {
            this.logger.Information("Starting ZeroMQ connection");

            var hostName = this.configuration.GetValue<string>("SmartFace:ZeroMQ:HostName", ZERO_MQ_DEFAULT_HOST);
            var port = this.configuration.GetValue<int>("SmartFace:ZeroMQ:Port", ZERO_MQ_DEFAULT_PORT);

            var reader = new ZeroMqNotificationReader(hostName, port, CancellationToken.None);

            // Log errors
            reader.OnError += (ex) => { logger.Error($"ERROR : {ex.Message}"); };

            // Hook on notification event
            reader.OnNotificationReceived += async (string topic, string json) =>
                    {
                        logger.Information($"Notification with topic: {topic} received.");
                    };

            // Start listening for ZeroMQ messages
            reader.Init();

            this.logger.Information($"ZeroMQ connect initialized at {reader.EndPoint}");
        }

        private void stopReceivingZeroMqotifications()
        {
            this.logger.Information("Stopping ZeroMQ connection");
        }
    }
}