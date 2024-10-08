using System;
using System.Linq;
using Serilog;
using SmartFace.AutoEnrollment.Models;

namespace SmartFace.AutoEnrollment.Service
{
    public class ValidationService
    {
        private readonly ILogger _logger;

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateFaceQuality = (input) =>
        {
            if (input.notification.FaceQuality == null)
            {
                return true;
            }

            if ((input.streamMapping.FaceQuality?.Min ?? 0) <= input.notification.FaceQuality)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateTemplateQuality = (input) =>
        {
            if (input.notification.TemplateQuality == null)
            {
                return true;
            }

            if ((input.streamMapping.TemplateQuality?.Min ?? 0) <= input.notification.TemplateQuality)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateFaceSize = (input) =>
        {
            if (input.notification.FaceSize == null)
            {
                return true;
            }

            if (
                (input.streamMapping.FaceSize?.Min ?? 0) <= input.notification.FaceSize &&
                (input.streamMapping.FaceSize?.Max ?? double.MaxValue) >= input.notification.FaceSize)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateFaceArea = (input) =>
        {
            if (input.notification.FaceArea == null)
            {
                return true;
            }

            if (
                (input.streamMapping.FaceArea?.Min ?? double.MinValue) <= input.notification.FaceArea &&
                (input.streamMapping.FaceArea?.Max ?? double.MaxValue) >= input.notification.FaceArea)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateFaceOrder = (input) =>
        {
            if (input.notification.FaceOrder == null)
            {
                return true;
            }

            if (
                (input.streamMapping.FaceOrder?.Min ?? 0) <= input.notification.FaceOrder &&
                (input.streamMapping.FaceOrder?.Max ?? double.MaxValue) >= input.notification.FaceOrder)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateFacesOnFrameCount = (input) =>
        {
            if (input.notification.FacesOnFrameCount == null)
            {
                return true;
            }

            if (
                (input.streamMapping.FacesOnFrameCount?.Min ?? 0) <= input.notification.FacesOnFrameCount &&
                (input.streamMapping.FacesOnFrameCount?.Max ?? double.MaxValue) >= input.notification.FacesOnFrameCount)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateBrightness = (input) =>
        {
            if (input.notification.Brightness == null)
            {
                return true;
            }

            if (
                (input.streamMapping.Brightness?.Min ?? double.MinValue) <= input.notification.Brightness &&
                (input.streamMapping.Brightness?.Max ?? double.MaxValue) >= input.notification.Brightness)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateSharpness = (input) =>
        {
            if (input.notification.Sharpness == null)
            {
                return true;
            }

            if (
                (input.streamMapping.Sharpness?.Min ?? double.MinValue) <= input.notification.Sharpness &&
                (input.streamMapping.Sharpness?.Max ?? double.MaxValue) >= input.notification.Sharpness)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateYawAngle = (input) =>
        {
            if (input.notification.YawAngle == null)
            {
                return true;
            }

            if (
                (input.streamMapping.YawAngle?.Min ?? double.MinValue) <= input.notification.YawAngle &&
                (input.streamMapping.YawAngle?.Max ?? double.MaxValue) >= input.notification.YawAngle)
            {
                return true;
            }

            return false;
        };


        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validateRollAngle = (input) =>
        {
            if (input.notification.RollAngle == null)
            {
                return true;
            }

            if (
                (input.streamMapping.RollAngle?.Min ?? double.MinValue) <= input.notification.RollAngle &&
                (input.streamMapping.RollAngle?.Max ?? double.MaxValue) >= input.notification.RollAngle)
            {
                return true;
            }

            return false;
        };


        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool> validatePitchAngle = (input) =>
        {
            if (input.notification.PitchAngle == null)
            {
                return true;
            }

            if (
                (input.streamMapping.PitchAngle?.Min ?? double.MinValue) <= input.notification.PitchAngle &&
                (input.streamMapping.PitchAngle?.Max ?? double.MaxValue) >= input.notification.PitchAngle)
            {
                return true;
            }

            return false;
        };

        private static readonly Func<(Notification notification, StreamConfiguration streamMapping), bool>[] validateAll = new[] {
            validateFaceQuality,
            validateTemplateQuality,
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

        public ValidationService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Validate(Notification notification, StreamConfiguration streamMapping)
        {
            _logger.Information("Face attributes: faceQuality {faceQuality}, templateQuality {templatequality}, faceSize {faceSize}, yawAngle {yawAngle}, rollAngle {rollAngle} pitchAngle {pitchAngle}", 
                                            notification.FaceQuality, notification.TemplateQuality, notification.FaceSize,
                                            notification.YawAngle, notification.RollAngle, notification.PitchAngle);

            var validationResults = new bool[validateAll.Length];

            for (var i = 0; i < validationResults.Length; i++)
            {
                var fn = validateAll[i];
                var isValid = fn.Invoke((notification, streamMapping));
                validationResults[i] = isValid;
            }

            _logger.Information("Validation result [{result}]", string.Join(',', validationResults.Select(s => s ? 1 : 0)));

            var allResult = validationResults.All(w => w);

            return allResult;
        }
    }
}