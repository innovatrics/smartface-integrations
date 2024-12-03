using SmartFace.AutoEnrollment.Service;
using Xunit;

namespace SmartFace.AutoEnrollment.Tests.Service
{
    public class CropCoordinatesValidatorTests
    {
        [Theory]
        [InlineData(1920, 1028, 121.31073760986328, 0, 236.39683532714844, 0, 121.31073760986328, 119.99687194824219, 236.39683532714844, 119.99687194824219, 0.0, true, true)]
        [InlineData(1920, 1028, 121.31073760986328, 0, 236.39683532714844, 0, 121.31073760986328, 119.99687194824219, 236.39683532714844, 119.99687194824219, 0.15, true, false)]
        [InlineData(1920, 1028, -2.58966064453125, -5.376869201660156, 183.6818084716797, -5.053672790527344, -3.0205841064453125, 242.98507690429688, 183.25088500976562, 243.3082733154297, 0.0, false, false)]
        [InlineData(1920, 1028, -2.58966064453125, -5.376869201660156, 183.6818084716797, -5.053672790527344, -3.0205841064453125, 242.98507690429688, 183.25088500976562, 243.3082733154297, 0.15, true, false)]
        public void IsImageWithinRange_ShouldReturnExpectedResult(
            int containerWidth, int containerHeight,
            double topLeftX, double topLeftY,
            double topRightX, double topRightY,
            double bottomLeftX, double bottomLeftY,
            double bottomRightX, double bottomRightY,
            double padding, bool isRelativePadding,
            bool expectedResult)
        {
            var result = CropCoordinatesValidator.IsImageWithinRange(
                containerWidth, containerHeight,
                topLeftX, topLeftY,
                topRightX, topRightY,
                bottomLeftX, bottomLeftY,
                bottomRightX, bottomRightY,
                padding, isRelativePadding
            );

            Assert.Equal(expectedResult, result);
        }
    }
}