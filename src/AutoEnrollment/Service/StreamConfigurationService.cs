using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using SmartFace.AutoEnrollment.Models;

namespace SmartFace.AutoEnrollment.Service
{
    public class StreamConfigurationService(IConfiguration configuration)
    {
        private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        public ICollection<StreamConfiguration> CreateMappings(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var config = _configuration.GetSection("Config").Get<Config>();

            config.Conditions = NormalizeConditionsWithDefaults(config.Conditions);

            var streamMapping = _configuration.GetSection("StreamConfigurations").Get<StreamConfiguration[]>();

            if (streamMapping == null)
            {
                streamMapping = Array.Empty<StreamConfiguration>();
            }

            streamMapping = streamMapping
                                .Where(w => w.StreamId == streamGuid)
                                .Select(s => NormalizeMappingWithDefaults(s, config))
                                .ToArray();

            if (streamMapping.Length == 0 && config.ApplyForAllStreams)
            {
                streamMapping = new[] {
                    NormalizeMappingWithDefaults(new StreamConfiguration(), config)
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

        private static StreamConfiguration NormalizeMappingWithDefaults(StreamConfiguration mapping, Config config)
        {
            mapping.FaceQuality ??= config.Conditions.FaceQuality;
            mapping.TemplateQuality ??= config.Conditions.TemplateQuality;
            mapping.FaceArea ??= config.Conditions.FaceArea;
            mapping.FaceOrder ??= config.Conditions.FaceOrder;
            mapping.FaceSize ??= config.Conditions.FaceSize;
            mapping.FacesOnFrameCount ??= config.Conditions.FacesOnFrameCount;
            mapping.Brightness ??= config.Conditions.Brightness;
            mapping.Sharpness ??= config.Conditions.Sharpness;
            mapping.KeepAutoLearn ??= config.Conditions.KeepAutoLearn;
            mapping.GroupDebounceMs ??= config.Conditions.GroupDebounceMs;
            mapping.StreamDebounceMs ??= config.Conditions.StreamDebounceMs;
            mapping.TrackletDebounceMs ??= config.Conditions.TrackletDebounceMs;
            mapping.YawAngle ??= config.Conditions.YawAngle;
            mapping.PitchAngle ??= config.Conditions.PitchAngle;
            mapping.RollAngle ??= config.Conditions.RollAngle;
            mapping.FramePaddingAbsolute ??= config.Conditions.FramePaddingAbsolute;
            mapping.FramePaddingRelative ??= config.Conditions.FramePaddingRelative;

            if (mapping.WatchlistIds == null || mapping.WatchlistIds?.Length == 0)
            {
                var ids1 = config.WatchlistIds ?? Array.Empty<string>();
                var ids2 = config.Conditions?.WatchlistIds ?? Array.Empty<string>();

                mapping.WatchlistIds = ids1.Union(ids2).Distinct().ToArray();
            }

            return mapping;
        }
    }
}
