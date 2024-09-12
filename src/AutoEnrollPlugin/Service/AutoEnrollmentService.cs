using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Factories;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class AutoEnrollmentService : IAutoEnrollmentService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IValidationService validationService;
        private readonly IStreamMappingService streamMappingService;

        public AutoEnrollmentService(
            ILogger logger,
            IConfiguration configuration,
            IValidationService validationServiceFactory,
            IStreamMappingService streamMappingService
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.validationService = validationServiceFactory ?? throw new ArgumentNullException(nameof(validationServiceFactory));
            this.streamMappingService = streamMappingService ?? throw new ArgumentNullException(nameof(streamMappingService));
        }

        public async Task ProcessNotificationAsync(Notification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var mappings = this.streamMappingService.CreateMappings(notification.StreamId);

            this.logger.Debug("Found {mappings} mappings for stream {stream}", mappings?.Count, notification.StreamId);

            foreach (var mapping in mappings)
            {
                var isValid = this.validationService.Validate(notification, mapping);

                if (isValid)
                {
                    //await this.EnrollAsync(Notification22);
                }
            }
        }
    }
}
