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
        private readonly INXWitnessAdapter nxWitnessAdapter;

        public ZeroMQNotificationProcessingService(
            ILogger logger,
            INXWitnessAdapter nxWitnessAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.nxWitnessAdapter = nxWitnessAdapter ?? throw new ArgumentNullException(nameof(nxWitnessAdapter));
        }

        public async Task ProcessNotificationAsync(string topic, string json)
        {
            switch (topic)
            {
                // case ZeroMqNotificationTopic.PEDESTRIANS_INSERTED:
                //         await this.nxWitnessAdapter.PushGenericEventAsync(
                //             caption: "Fall detection",
                //             cameraRef: $"DS-2CD2043G0-I"
                //         );
                //         break;

                case ZeroMqNotificationTopic.HUMAN_FALL_DETECTED:
                    {
                        var dto = JsonConvert.DeserializeObject<HumanFallDetectionNotificationDTO>(json);
                        await this.nxWitnessAdapter.PushGenericEventAsync(
                            timestamp: dto.FrameTimestamp,
                            caption: "Fall detection",
                            cameraRef: $"{dto.StreamId}"
                        );
                        break;
                    }

                default:
                    break;
            }
        }
    }
}