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
    internal class ZeroMQNotificationProcessingService : IZeroMQNotificationProcessingService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IBridge bridge;

        public ZeroMQNotificationProcessingService(
            ILogger logger,
            IConfiguration configuration,
            IBridge bridge
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public async Task ProcessNotificationAsync(string topic, string json)
        {
            switch (topic)
            {
                case ZeroMqNotificationTopic.HUMAN_FALL_DETECTED:
                    var caption = this.configuration.GetValue<string>("", ZeroMqNotificationTopic.HUMAN_FALL_DETECTED);

                    var dto = JsonConvert.DeserializeObject<HumanFallDetectionNotificationDTO>(json);

                    await this.bridge.PushGenericEventAsync(
                        timestamp: dto.FrameTimestamp,
                        caption: caption,
                        streamId: dto.StreamId
                    );
                    break;


                default:
                    break;
            }
        }
    }
}