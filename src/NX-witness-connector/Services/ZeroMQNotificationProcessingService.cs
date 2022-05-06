using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

using Innovatrics.SmartFace.Integrations.Shared.ZeroMQ;
using Innovatrics.SmartFace.Models.Notifications;
using Innovatrics.SmartFace.Integrations.NXWitnessConnector.Models;
using System.Linq;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    internal class ZeroMQNotificationProcessingService : IZeroMQNotificationProcessingService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IBridge bridge;
        private readonly Dictionary<Guid, DateTime> lastOpenedByWlMemberPassage;

        public ZeroMQNotificationProcessingService(
            ILogger logger,
            IConfiguration configuration,
            IBridge bridge
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            this.lastOpenedByWlMemberPassage = new Dictionary<Guid, DateTime>();
        }

        public async Task ProcessNotificationAsync(string topic, string json)
        {
            var eventConfig = this.getEventConfig(topic);

            switch (topic)
            {
                case ZeroMqNotificationTopic.HUMAN_FALL_DETECTED:
                    var dto = JsonConvert.DeserializeObject<HumanFallDetectionNotificationDTO>(json);

                    var debountceMs = eventConfig?.DebounceMs;
                    
                    if (debountceMs != null && debountceMs > 0)
                    {
                        if (!this.isDebounceExpired(dto.StreamId, debountceMs.Value))
                        {
                            this.logger.Information("{topic} blocked by debounce period {}ms.", topic, debountceMs);
                            return;
                        }

                        this.setDebounceUtcNow(dto.StreamId);
                    }

                    await this.bridge.PushGenericEventAsync(
                        timestamp: dto.FrameTimestamp,
                        caption: eventConfig?.Caption ?? ZeroMqNotificationTopic.HUMAN_FALL_DETECTED,
                        streamId: dto.StreamId
                    );
                    break;


                default:
                    break;
            }
        }

        private EventConfig getEventConfig(string topic)
        {
            var eventConfigs = this.configuration.GetSection("Events").Get<EventConfig[]>();
            return eventConfigs
                        .Where(w => w.Topic == topic)
                        .SingleOrDefault();
        }

        private bool isDebounceExpired(Guid streamId, int debounceMs)
        {
            bool isActive;

            if (lastOpenedByWlMemberPassage.ContainsKey(streamId))
            {
                var difference = DateTime.UtcNow - lastOpenedByWlMemberPassage[streamId];
                if (difference < TimeSpan.FromMilliseconds(debounceMs))
                {
                    this.logger.Debug("Time difference is smaller than {debounceMs} milliseconds, it is {Difference}.", debounceMs, difference);
                    isActive = false;
                }
                else
                {
                    this.logger.Debug("Time difference is larger than {debounceMs} milliseconds, it is {Difference}.", debounceMs, difference);
                    isActive = true;
                }
            }
            else
            {
                this.logger.Debug("No openings in the given time interval ({debounceMs} milliseconds), allow.", debounceMs);
                isActive = true;
            }

            return isActive;
        }

        private void setDebounceUtcNow(Guid streamId)
        {
            this.lastOpenedByWlMemberPassage[streamId] = DateTime.UtcNow;
        }
    }
}