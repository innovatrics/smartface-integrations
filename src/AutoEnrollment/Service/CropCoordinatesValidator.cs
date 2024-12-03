using System;
using System.Linq;
using Serilog;
using SmartFace.AutoEnrollment.Models;

namespace SmartFace.AutoEnrollment.Service
{
    public class CropCoordinatesValidator
    {
        private readonly ILogger _logger;

        public CropCoordinatesValidator(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Validate(Notification notification, StreamConfiguration streamMapping)
        {
            if (streamMapping.FramePaddingAbsolute == null && streamMapping.FramePaddingRelative == null)
            {
                return true;
            }

            _logger.Information("CropCoordinates: {cropLeftTopX}, {cropLeftTopY}, {cropRightTopX}, {cropRightTopY}, {cropLeftBottomX}, {cropLeftBottomY},{cropRightBottomX}, {cropRightBottomY}, {absolute}, {relative}",
                                    notification.CropCoordinates.CropLeftTopX, notification.CropCoordinates.CropLeftTopY,
                                    notification.CropCoordinates.CropRightTopX, notification.CropCoordinates.CropRightTopY,
                                    notification.CropCoordinates.CropLeftBottomX, notification.CropCoordinates.CropLeftBottomY,
                                    notification.CropCoordinates.CropRightBottomX, notification.CropCoordinates.CropRightBottomY,
                                    streamMapping.FramePaddingAbsolute, streamMapping.FramePaddingRelative
                                );
            
            
            return IsImageWithinRange(
                notification.FrameInformation.Width, notification.FrameInformation.Height,
                notification.CropCoordinates.CropLeftTopX, notification.CropCoordinates.CropLeftTopY,
                notification.CropCoordinates.CropRightTopX, notification.CropCoordinates.CropRightTopY,
                notification.CropCoordinates.CropLeftBottomX, notification.CropCoordinates.CropLeftBottomY,
                notification.CropCoordinates.CropRightBottomX, notification.CropCoordinates.CropRightBottomY,
                streamMapping.FramePaddingRelative ?? streamMapping.FramePaddingAbsolute ?? 0.0,
                streamMapping.FramePaddingRelative != null);
        }

        public static bool IsImageWithinRange(
            int containerWidth, int containerHeight,
            double topLeftX, double topLeftY,
            double topRightX, double topRightY,
            double bottomLeftX, double bottomLeftY,
            double bottomRightX, double bottomRightY,
            double padding = 0.0,
            bool isRelativePadding = false
        )
        {
            // Calculate absolute padding if padding is relative
            double paddingX = isRelativePadding ? containerWidth * padding : padding;
            double paddingY = isRelativePadding ? containerHeight * padding : padding;

            // Calculate padded bounds
            double paddedLeft = paddingX;
            double paddedTop = paddingY;
            double paddedRight = containerWidth - paddingX;
            double paddedBottom = containerHeight - paddingY;

            // Check if all corners are within the padded bounds
            return
                topLeftX >= paddedLeft && topLeftY >= paddedTop &&
                topRightX <= paddedRight && topRightY >= paddedTop &&
                bottomLeftX >= paddedLeft && bottomLeftY <= paddedBottom &&
                bottomRightX <= paddedRight && bottomRightY <= paddedBottom;
        }
    }
}