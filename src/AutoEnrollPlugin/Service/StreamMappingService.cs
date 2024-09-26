using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class StreamMappingService
    {
        private readonly IConfiguration _configuration;

        public StreamMappingService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public ICollection<StreamMapping> CreateMappings(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var config = _configuration.GetSection("Config").Get<Config>();

            config.Conditions = NormalizeConditionsWithDefaults(config.Conditions);

            var streamMapping = _configuration.GetSection("StreamMappings").Get<StreamMapping[]>();

            if (streamMapping == null)
            {
                streamMapping = Array.Empty<StreamMapping>();
            }

            streamMapping = streamMapping
                                .Where(w => w.StreamId == streamGuid)
                                .Select(s => NormalizeMappingWithDefaults(s, config.Conditions))
                                .ToArray();

            if (streamMapping.Length == 0 && config.ApplyForAllStreams)
            {
                streamMapping = new[] {
                    NormalizeMappingWithDefaults(new StreamMapping(), config.Conditions)
                };
            }

            return streamMapping;
        }

        private static Conditions NormalizeConditionsWithDefaults(Conditions conditions)
        {
            conditions ??= new Conditions();

            conditions.FaceQuality ??= new Range<int?>()
            {
                Min = 2000
            };

            conditions.TemplateQuality ??= new Range<int?>()
            {
                Min = 80,
            };

            conditions.YawAngle ??= new Range<double?>()
            {
                Min = -7,
                Max = 7
            };

            conditions.PitchAngle ??= new Range<double?>()
            {
                Min = -25,
                Max = 25
            };

            conditions.RollAngle ??= new Range<double?>()
            {
                Min = -15,
                Max = 15
            };

            return conditions;
        }

        private static StreamMapping NormalizeMappingWithDefaults(StreamMapping mapping, Conditions config)
        {
            mapping.FaceQuality ??= config.FaceQuality;
            mapping.TemplateQuality ??= config.TemplateQuality;
            mapping.FaceArea ??= config.FaceArea;
            mapping.FaceOrder ??= config.FaceOrder;
            mapping.FaceSize ??= config.FaceSize;
            mapping.FacesOnFrameCount ??= config.FacesOnFrameCount;
            mapping.Brightness ??= config.Brightness;
            mapping.Sharpness ??= config.Sharpness;
            mapping.KeepAutoLearn ??= config.KeepAutoLearn;
            mapping.GroupDebounceMs ??= config.GroupDebounceMs;
            mapping.StreamDebounceMs ??= config.StreamDebounceMs;
            mapping.TrackletDebounceMs ??= config.TrackletDebounceMs;
            mapping.YawAngle ??= config.YawAngle;
            mapping.PitchAngle ??= config.PitchAngle;
            mapping.RollAngle ??= config.RollAngle;

            if (mapping.WatchlistIds == null || mapping.WatchlistIds?.Length == 0)
            {
                mapping.WatchlistIds = config.WatchlistIds;
            }

            return mapping;
        }
    }
}
