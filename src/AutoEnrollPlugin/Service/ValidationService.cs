using System;
using System.Linq;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class ValidationService : IValidationService
    {
        private static Func<(Notification notification, StreamMapping streamMapping), bool> validateDetectionQuality = (input) =>
        {
            if (input.notification.DetectionQuality == null)
            {
                return true;
            }

            if ((input.streamMapping.MinDetectionQuality ?? 0) <= input.notification.DetectionQuality)
            {
                return true;
            }

            return false;
        };

        private static Func<(Notification notification, StreamMapping streamMapping), bool> validateExtractionQuality = (input) =>
        {
            if (input.notification.ExtractionQuality == null)
            {
                return true;
            }

            if ((input.streamMapping.MinExtractionQuality ?? 0) <= input.notification.ExtractionQuality)
            {
                return true;
            }

            return false;
        };

        private static Func<(Notification notification, StreamMapping streamMapping), bool>[] validateAll = new[] {
            validateDetectionQuality,
            validateExtractionQuality,
        };

        public bool Validate(Notification notification, StreamMapping streamMapping)
        {
            var allResult = validateAll.Select(s => s.Invoke((notification, streamMapping))).All(w => w == true);
            return allResult;
        }
    }
}