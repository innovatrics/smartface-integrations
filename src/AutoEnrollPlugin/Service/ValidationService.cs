using System;
using System.Linq;
using Serilog;

using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class ValidationService : IValidationService
    {
        private readonly ILogger logger;

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateDetectionQuality = (input) =>
        {
            if (input.notification.DetectionQuality == null)
            {
                return true;
            }

            if ((input.streamMapping.DetectionQuality?.Min ?? 0) <= input.notification.DetectionQuality)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateExtractionQuality = (input) =>
        {
            if (input.notification.ExtractionQuality == null)
            {
                return true;
            }

            if ((input.streamMapping.ExtractionQuality?.Min ?? 0) <= input.notification.ExtractionQuality)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateFaceSize = (input) =>
        {
            if (input.notification.FaceSize == null)
            {
                return true;
            }

            if (
                (input.streamMapping.FaceSize?.Min ?? 0) <= input.notification.FaceSize &&
                (input.streamMapping.FaceSize?.Max ?? Double.MaxValue) >= input.notification.FaceSize)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateFaceArea = (input) =>
        {
            if (input.notification.FaceArea == null)
            {
                return true;
            }

            if (
                (input.streamMapping.FaceArea?.Min ?? Double.MinValue) <= input.notification.FaceArea &&
                (input.streamMapping.FaceArea?.Max ?? Double.MaxValue) >= input.notification.FaceArea)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateFaceOrder = (input) =>
        {
            if (input.notification.FaceOrder == null)
            {
                return true;
            }

            if (
                (input.streamMapping.FaceOrder?.Min ?? 0) <= input.notification.FaceOrder &&
                (input.streamMapping.FaceOrder?.Max ?? Double.MaxValue) >= input.notification.FaceOrder)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateFacesOnFrameCount = (input) =>
        {
            if (input.notification.FacesOnFrameCount == null)
            {
                return true;
            }

            if (
                (input.streamMapping.FacesOnFrameCount?.Min ?? 0) <= input.notification.FacesOnFrameCount &&
                (input.streamMapping.FacesOnFrameCount?.Max ?? Double.MaxValue) >= input.notification.FacesOnFrameCount)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateBrightness = (input) =>
        {
            if (input.notification.Brightness == null)
            {
                return true;
            }

            if (
                (input.streamMapping.Brightness?.Min ?? Double.MinValue) <= input.notification.Brightness &&
                (input.streamMapping.Brightness?.Max ?? Double.MaxValue) >= input.notification.Brightness)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateSharpness = (input) =>
        {
            if (input.notification.Sharpness == null)
            {
                return true;
            }

            if (
                (input.streamMapping.Sharpness?.Min ?? Double.MinValue) <= input.notification.Sharpness &&
                (input.streamMapping.Sharpness?.Max ?? Double.MaxValue) >= input.notification.Sharpness)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateYawAngle = (input) =>
        {
            if (input.notification.YawAngle == null)
            {
                return true;
            }

            if (
                (input.streamMapping.YawAngle?.Min ?? Double.MinValue) <= input.notification.YawAngle &&
                (input.streamMapping.YawAngle?.Max ?? Double.MaxValue) >= input.notification.YawAngle)
            {
                return true;
            }

            return false;
        };


        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validateRollAngle = (input) =>
        {
            if (input.notification.RollAngle == null)
            {
                return true;
            }

            if (
                (input.streamMapping.RollAngle?.Min ?? Double.MinValue) <= input.notification.RollAngle &&
                (input.streamMapping.RollAngle?.Max ?? Double.MaxValue) >= input.notification.RollAngle)
            {
                return true;
            }

            return false;
        };


        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool> validatePitchAngle = (input) =>
        {
            if (input.notification.PitchAngle == null)
            {
                return true;
            }

            if (
                (input.streamMapping.PitchAngle?.Min ?? Double.MinValue) <= input.notification.PitchAngle &&
                (input.streamMapping.PitchAngle?.Max ?? Double.MaxValue) >= input.notification.PitchAngle)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamMapping streamMapping), bool>[] validateAll = new[] {
            validateDetectionQuality,
            validateExtractionQuality,
            validateFaceSize,
            validateFaceArea,
            validateFaceOrder,
            validateFacesOnFrameCount,
            validateBrightness,
            validateSharpness,
            validateYawAngle,
            validateRollAngle,
            validatePitchAngle
        };

        public ValidationService(
            ILogger logger
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Validate(Notification notification, StreamMapping streamMapping)
        {
            var validationResults = new bool[validateAll.Length];

            for (var i = 0; i < validationResults.Length; i++)
            {
                var fn = validateAll[i];
                var isValid = fn.Invoke((notification, streamMapping));
                validationResults[i] = isValid;
            }

            this.logger.Information("Validation result [{result}]", string.Join(',', validationResults.Select(s => s ? 1 : 0)));

            var allResult = validationResults
                                .All(w => w == true);

            return allResult;
        }
    }
}