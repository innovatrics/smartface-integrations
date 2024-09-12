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
        private readonly IValidationServiceFactory validationServiceFactory;

        public AutoEnrollmentService(
            ILogger logger,
            IConfiguration configuration,
            IValidationServiceFactory validationServiceFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.validationServiceFactory = validationServiceFactory ?? throw new ArgumentNullException(nameof(validationServiceFactory));
        }

        public async Task ProcessNotificationAsync(Notification22 notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var validationService = this.validationServiceFactory.Create(notification.StreamId);

            var isValid = validationService.ValidateNotification(notification);

            if (isValid)
            {
                //await this.EnrollAsync(Notification22);
            }
        }
    }
}
