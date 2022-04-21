using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

using Innovatrics.SmartFace.Integrations.Shared.ZeroMQ;
using Innovatrics.SmartFace.Models.Notifications;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    internal class WorkerService : IHostedService
    {
        private const string ZERO_MQ_DEFAULT_HOST = "localhost";
        private const int ZERO_MQ_DEFAULT_PORT = 2406;

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
            
            var hostName = this.configuration.GetValue<string>("SmartFace:ZeroMQ:HostName", ZERO_MQ_DEFAULT_HOST);
            var port = this.configuration.GetValue<int>("SmartFace:ZeroMQ:Port", ZERO_MQ_DEFAULT_PORT);

            this.zeroMqNotificationReader = new ZeroMqNotificationReader(hostName, port, CancellationToken.None);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(WorkerService)} is starting");

            this.startReceivingZeroMqotifications();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Information($"{nameof(WorkerService)} is stopping");

            this.stopReceivingZeroMqotifications();
        }

        private void startReceivingZeroMqotifications()
        {
            this.logger.Information("Starting ZeroMQ connection");

            // Log errors
            this.zeroMqNotificationReader.OnError += (ex) => { logger.Error($"ERROR : {ex.Message}"); };

            // Hook on notification event
            this.zeroMqNotificationReader.OnNotificationReceived += (topic, json) =>
            {
                logger.Information($"Notification with topic : {topic} received.");

                switch (topic)
                {
                    case ZeroMqNotificationTopic.HUMAN_FALL_DETECTED:
                        {
                            var dto = JsonConvert.DeserializeObject<HumanFallDetectionNotificationDTO>(json);
                            break;
                        }

                    default:
                        break;
                }

            };

            // Start listening for ZeroMQ messages
            this.zeroMqNotificationReader.Init();

            this.logger.Information($"ZeroMQ connect initialized at {this.zeroMqNotificationReader.EndPoint}");
        }
        
        private void stopReceivingZeroMqotifications()
        {
            this.logger.Information("Stopping ZeroMQ connection");

            this.zeroMqNotificationReader.Dispose();
        }
    }
}