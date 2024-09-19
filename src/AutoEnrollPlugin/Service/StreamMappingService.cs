using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Factories;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class StreamMappingService : IStreamMappingService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;

        public StreamMappingService(
            ILogger logger,
            IConfiguration configuration
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public ICollection<StreamMapping> CreateMappings(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var config = configuration.GetSection("Config").Get<Config>();

            config.Conditions = this.normalizeConditionsWithDefaults(config.Conditions);

            var streamMapping = this.configuration.GetSection("StreamMappings").Get<StreamMapping[]>();

            if (streamMapping == null)
            {
                streamMapping = Array.Empty<StreamMapping>();
            }

            streamMapping = streamMapping
                                .Where(w => w.StreamId == streamGuid)
                                .Select(s => this.normalizeMappingWithDefaults(s, config.Conditions))
                                .ToArray();

            if (streamMapping.Length == 0 && config.ApplyForAllStreams)
            {
                streamMapping = new[] {
                    this.normalizeMappingWithDefaults(new StreamMapping(), config.Conditions)
                };
            }

            return streamMapping;
        }

        private Conditions normalizeConditionsWithDefaults(Conditions conditions)
        {
            if (conditions == null)
            {
                conditions = new Conditions();
            }

            if (conditions.FaceQuality == null)
            {
                conditions.FaceQuality = new Range<int?>()
                {
                    Min = 2000
                };
            }

            if (conditions.TemplateQuality == null)
            {
                conditions.TemplateQuality = new Range<int?>()
                {
                    Min = 80,
                };
            }

            if (conditions.YawAngle == null)
            {
                conditions.YawAngle = new Range<double?>()
                {
                    Min = -7,
                    Max = 7
                };
            }

            if (conditions.PitchAngle == null)
            {
                conditions.PitchAngle = new Range<double?>()
                {
                    Min = -25,
                    Max = 25
                };
            }

            if (conditions.RollAngle == null)
            {
                conditions.RollAngle = new Range<double?>()
                {
                    Min = -15,
                    Max = 15
                };
            }

            return conditions;
        }

        private StreamMapping normalizeMappingWithDefaults(StreamMapping mapping, Conditions config)
        {
            if (mapping.FaceQuality == null)
            {
                mapping.FaceQuality = config.FaceQuality;
            }

            if (mapping.TemplateQuality == null)
            {
                mapping.TemplateQuality = config.TemplateQuality;
            }

            if (mapping.FaceArea == null)
            {
                mapping.FaceArea = config.FaceArea;
            }

            if (mapping.FaceOrder == null)
            {
                mapping.FaceOrder = config.FaceOrder;
            }

            if (mapping.FaceSize == null)
            {
                mapping.FaceSize = config.FaceSize;
            }

            if (mapping.FacesOnFrameCount == null)
            {
                mapping.FacesOnFrameCount = config.FacesOnFrameCount;
            }

            if (mapping.Brightness == null)
            {
                mapping.Brightness = config.Brightness;
            }

            if (mapping.Sharpness == null)
            {
                mapping.Sharpness = config.Sharpness;
            }

            if (mapping.WatchlistIds == null || mapping.WatchlistIds?.Length == 0)
            {
                mapping.WatchlistIds = config.WatchlistIds;
            }

            if (mapping.KeepAutoLearn == null)
            {
                mapping.KeepAutoLearn = config.KeepAutoLearn;
            }

            if (mapping.GroupDebounceMs == null)
            {
                mapping.GroupDebounceMs = config.GroupDebounceMs;
            }

            if (mapping.StreamDebounceMs == null)
            {
                mapping.StreamDebounceMs = config.StreamDebounceMs;
            }

            if (mapping.TrackletDebounceMs == null)
            {
                mapping.TrackletDebounceMs = config.TrackletDebounceMs;
            }

            if (mapping.YawAngle == null)
            {
                mapping.YawAngle = config.YawAngle;
            }

            if (mapping.PitchAngle == null)
            {
                mapping.PitchAngle = config.PitchAngle;
            }

            if (mapping.RollAngle == null)
            {
                mapping.RollAngle = config.RollAngle;
            }

            return mapping;
        }
    }
}
