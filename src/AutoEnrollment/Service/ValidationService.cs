using System;
using System.Linq;
using System.Reflection;
using Serilog;
using SmartFace.AutoEnrollment.Models;

namespace SmartFace.AutoEnrollment.Service
{
    public class ValidationService
    {
        private readonly ILogger _logger;

        private static bool ValidateFaceQuality(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.FaceQuality == null)
            {
                return true;
            }

            if ((streamMapping.FaceQuality?.Min ?? 0) <= notification.FaceQuality)
            {
                return true;
            }

            return false;
        }

        private static bool ValidateTemplateQuality(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.TemplateQuality == null)
            {
                return true;
            }

            if ((streamMapping.TemplateQuality?.Min ?? 0) <= notification.TemplateQuality)
            {
                return true;
            }

            return false;
        }

        private static bool ValidateFaceSize(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.FaceSize == null)
            {
                return true;
            }

            if (
                (streamMapping.FaceSize?.Min ?? 0) <= notification.FaceSize &&
                (streamMapping.FaceSize?.Max ?? double.MaxValue) >= notification.FaceSize)
            {
                return true;
            }

            return false;
        }

        private static bool ValidateFaceArea(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.FaceArea == null)
            {
                return true;
            }

            if (
                (streamMapping.FaceArea?.Min ?? double.MinValue) <= notification.FaceArea &&
                (streamMapping.FaceArea?.Max ?? double.MaxValue) >= notification.FaceArea)
            {
                return true;
            }

            return false;
        }

        private static bool ValidateFaceOrder(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.FaceOrder == null)
            {
                return true;
            }

            if (
                (streamMapping.FaceOrder?.Min ?? 0) <= notification.FaceOrder &&
                (streamMapping.FaceOrder?.Max ?? double.MaxValue) >= notification.FaceOrder)
            {
                return true;
            }

            return false;
        }

        private static bool ValidateFacesOnFrameCount(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.FacesOnFrameCount == null)
            {
                return true;
            }

            if (
                (streamMapping.FacesOnFrameCount?.Min ?? 0) <= notification.FacesOnFrameCount &&
                (streamMapping.FacesOnFrameCount?.Max ?? double.MaxValue) >= notification.FacesOnFrameCount)
            {
                return true;
            }

            return false;
        }

        private static bool ValidateBrightness(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.Brightness == null)
            {
                return true;
            }

            if (
                (streamMapping.Brightness?.Min ?? double.MinValue) <= notification.Brightness &&
                (streamMapping.Brightness?.Max ?? double.MaxValue) >= notification.Brightness)
            {
                return true;
            }

            return false;
        }

        private static bool ValidateSharpness(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.Sharpness == null)
            {
                return true;
            }

            if (
                (streamMapping.Sharpness?.Min ?? double.MinValue) <= notification.Sharpness &&
                (streamMapping.Sharpness?.Max ?? double.MaxValue) >= notification.Sharpness)
            {
                return true;
            }

            return false;
        }

        private static bool ValidateYawAngle(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.YawAngle == null)
            {
                return true;
            }

            if (
                (streamMapping.YawAngle?.Min ?? double.MinValue) <= notification.YawAngle &&
                (streamMapping.YawAngle?.Max ?? double.MaxValue) >= notification.YawAngle)
            {
                return true;
            }

            return false;
        }

        private static bool ValidateRollAngle(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.RollAngle == null)
            {
                return true;
            }

            if (
                (streamMapping.RollAngle?.Min ?? double.MinValue) <= notification.RollAngle &&
                (streamMapping.RollAngle?.Max ?? double.MaxValue) >= notification.RollAngle)
            {
                return true;
            }

            return false;
        }

        private static bool ValidatePitchAngle(Notification notification, StreamConfiguration streamMapping)
        {
            if (notification.PitchAngle == null)
            {
                return true;
            }

            if (
                (streamMapping.PitchAngle?.Min ?? double.MinValue) <= notification.PitchAngle &&
                (streamMapping.PitchAngle?.Max ?? double.MaxValue) >= notification.PitchAngle)
            {
                return true;
            }

            return false;
        }

        private static readonly Func<Notification, StreamConfiguration, bool>[] validateAllFunctions = new[] {
            ValidateFaceQuality,
            ValidateTemplateQuality,
            ValidateFaceSize,
            ValidateFaceArea,
            ValidateFaceOrder,
            ValidateFacesOnFrameCount,
            ValidateBrightness,
            ValidateSharpness,
            ValidateYawAngle,
            ValidateRollAngle,
            ValidatePitchAngle
        };

        public ValidationService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Validate(Notification notification, StreamConfiguration streamMapping)
        {
            _logger.Information("Face attributes: faceQuality {faceQuality}, templateQuality {templatequality}, faceSize {faceSize}, yawAngle {yawAngle}, rollAngle {rollAngle} pitchAngle {pitchAngle}",
                                            notification.FaceQuality, notification.TemplateQuality, notification.FaceSize,
                                            notification.YawAngle, notification.RollAngle, notification.PitchAngle);

            var validationResults = new bool[validateAllFunctions.Length];

            for (var i = 0; i < validationResults.Length; i++)
            {
                var fn = validateAllFunctions[i];

                var isValid = fn.Invoke(notification, streamMapping);
                validationResults[i] = isValid;

                if (!isValid)
                {
                    var methodInfo = fn.GetMethodInfo();

                    if (methodInfo?.Name != null)
                    {
                        _logger.Information($"{methodInfo?.Name} failed");
                    }
                }
            }

            _logger.Information("Validation result [{result}]", string.Join(',', validationResults.Select(s => s ? 1 : 0)));

            var allResult = validationResults.All(w => w);

            return allResult;
        }
    }
}