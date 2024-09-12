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
                return new StreamMapping[] { };
            }

            return streamMapping
                        .Where(w => w.StreamId == streamGuid)
                        .ToArray();

        }
    }
}
