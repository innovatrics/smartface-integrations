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

            var streamMapping = this.configuration.GetSection("StreamMappings").Get<StreamMapping[]>();

            if (streamMapping == null)
            {
                streamMapping = Array.Empty<StreamMapping>();
            }

            return streamMapping
                        .Where(w => w.StreamId == streamGuid)
                        .Select(s => this.normalizeMappingWithDefaults(s))
                        .ToArray();

        }

        private StreamMapping normalizeMappingWithDefaults(StreamMapping mapping)
        {
            if (mapping.FaceQuality == null)
            {
                mapping.FaceQuality = new Range<int?>()
                {
                    Min = this.configuration.GetValue<int>("Config:FaceQuality:Min", 2000)
                };
            }

            if (mapping.TemplateQuality == null)
            {
                mapping.TemplateQuality = new Range<int?>()
                {
                    Min = this.configuration.GetValue<int>("Config:TemplateQuality:Min", 80)
                };
            }

            if (mapping.YawAngle == null)
            {
                mapping.YawAngle = new Range<double?>()
                {
                    Min = this.configuration.GetValue<double>("Config:YawAngle:Min", -7),
                    Max = this.configuration.GetValue<double>("Config:YawAngle:Max", 7)
                };
            }

            if (mapping.PitchAngle == null)
            {
                mapping.PitchAngle = new Range<double?>()
                {
                    Min = this.configuration.GetValue<double>("Config:PitchAngle:Min", -25),
                    Max = this.configuration.GetValue<double>("Config:PitchAngle:Max", 25)
                };
            }

            if (mapping.RollAngle == null)
            {
                mapping.RollAngle = new Range<double?>()
                {
                    Min = this.configuration.GetValue<double>("Config:RollAngle:Min", -15),
                    Max = this.configuration.GetValue<double>("Config:RollAngle:Max", 15)
                };
            }

            return mapping;
        }
    }
}
