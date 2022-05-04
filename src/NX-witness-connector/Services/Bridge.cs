using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.NXWitnessConnector.Models;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    public class Bridge : IBridge
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly INXWitnessAdapter nxWitnessAdapter;

        public Bridge(
            ILogger logger,
            IConfiguration configuration,
            INXWitnessAdapter nxWitnessAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.nxWitnessAdapter = nxWitnessAdapter ?? throw new ArgumentNullException(nameof(nxWitnessAdapter));
        }

        public async Task PushGenericEventAsync(
            DateTime? timestamp = null,
            string source = null,
            string caption = null,
            Guid? streamId = null
        )
        {

            var nxWitnessCameraId = this.convertToCameraId(streamId);

            if (nxWitnessCameraId == null)
            {
                this.logger.Information("Stream has not any mapping to NX Witness Camera. StreamId {streamId}", streamId);
                return;
            }

            await this.nxWitnessAdapter.PushGenericEventAsync(
                            timestamp: timestamp,
                            caption: caption,
                            source: source,
                            cameraRef: nxWitnessCameraId
                        );
        }

        private string convertToCameraId(Guid? streamId)
        {
            var cameraMappings = this.configuration.GetSection("NXWitnessConnector:Cameras").Get<CameraMappingConfig[]>();

            if (cameraMappings == null)
            {
                return $"cam_{streamId}";
            }

            return cameraMappings
                        .Where(w => w.Source == streamId)
                        .Select(s => s.Target?.ToLower())
                        .SingleOrDefault();
        }
    }
}