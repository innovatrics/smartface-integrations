using GraphQL;
using Serilog;
using Serilog.Core;
using SmartFace.AutoEnrollment.Models;
using SmartFace.AutoEnrollment.NotificationReceivers;
using SmartFace.AutoEnrollment.Service;
using Xunit;

namespace SmartFace.AutoEnrollment.Tests.Service
{
    public class ValidationServiceTests
    {
        public const string NOT_IDENTIFIED_VALID = @"{
                                ""data"": {
                                    ""identificationEvent"": {
                                    ""identificationEventType"": ""NOT_IDENTIFIED"",
                                    ""streamInformation"": {
                                        ""streamId"": ""f945286a-1bb1-494b-a806-ab4aebf5ad9d""
                                    },
                                    ""frameInformation"": {
                                        ""width"": 1020,
                                        ""height"": 600
                                    },
                                    ""modality"": ""FACE"",
                                    ""faceModalityInfo"": {
                                        ""faceInformation"": {
                                        ""id"": ""f59aa662-9202-4796-9a73-1ba9314d6b43"",
                                        ""trackletId"": ""8e50ec69-1f63-487f-8c12-a35996793b5f"",
                                        ""cropImage"": null,
                                        ""cropCoordinates"": {
                                            ""cropLeftTopX"": 211.83380126953125,
                                            ""cropLeftTopY"": 151.6326141357422,
                                            ""cropLeftBottomX"": 211.83380126953125,
                                            ""cropLeftBottomY"": 275.91845703125,
                                            ""cropRightTopX"": 320.2796325683594,
                                            ""cropRightTopY"": 151.6326141357422,
                                            ""cropRightBottomX"": 320.2796325683594,
                                            ""cropRightBottomY"": 275.91845703125
                                        },
                                        ""faceArea"": 0.05159984156489372,
                                        ""faceSize"": 38.474281311035156,
                                        ""faceOrder"": 1,
                                        ""facesOnFrameCount"": 1,
                                        ""faceMaskStatus"": ""NO_MASK"",
                                        ""faceQuality"": 2142,
                                        ""templateQuality"": 187,
                                        ""sharpness"": -10000,
                                        ""brightness"": -4964,
                                        ""yawAngle"": 39.31890106201172,
                                        ""rollAngle"": 26.746726989746094,
                                        ""pitchAngle"": 1.9021213054656982
                                        }
                                    }
                                    }
                                }
                            }";

        public const string NOT_IDENTIFIED_INVALID_AT_BORDER = @"{
                                ""data"": {
                                    ""identificationEvent"": {
                                    ""identificationEventType"": ""NOT_IDENTIFIED"",
                                    ""streamInformation"": {
                                        ""streamId"": ""f945286a-1bb1-494b-a806-ab4aebf5ad9d""
                                    },
                                    ""frameInformation"": {
                                        ""width"": 1020,
                                        ""height"": 600
                                    },
                                    ""modality"": ""FACE"",
                                    ""faceModalityInfo"": {
                                        ""faceInformation"": {
                                        ""id"": ""f59aa662-9202-4796-9a73-1ba9314d6b43"",
                                        ""trackletId"": ""8e50ec69-1f63-487f-8c12-a35996793b5f"",
                                        ""cropImage"": null,
                                        ""cropCoordinates"": {
                                            ""cropLeftTopX"": 211.83380126953125,
                                            ""cropLeftTopY"": 0,
                                            ""cropLeftBottomX"": 211.83380126953125,
                                            ""cropLeftBottomY"": 275.91845703125,
                                            ""cropRightTopX"": 320.2796325683594,
                                            ""cropRightTopY"": 0,
                                            ""cropRightBottomX"": 320.2796325683594,
                                            ""cropRightBottomY"": 275.91845703125
                                        },
                                        ""faceArea"": 0.05159984156489372,
                                        ""faceSize"": 38.474281311035156,
                                        ""faceOrder"": 1,
                                        ""facesOnFrameCount"": 1,
                                        ""faceMaskStatus"": ""NO_MASK"",
                                        ""faceQuality"": 2142,
                                        ""templateQuality"": 187,
                                        ""sharpness"": -10000,
                                        ""brightness"": -4964,
                                        ""yawAngle"": 39.31890106201172,
                                        ""rollAngle"": 26.746726989746094,
                                        ""pitchAngle"": 1.9021213054656982
                                        }
                                    }
                                    }
                                }
                            }";


        [Theory]
        [InlineData(NOT_IDENTIFIED_VALID, true, 0)]
        [InlineData(NOT_IDENTIFIED_INVALID_AT_BORDER, false, 0.15)]
        public void IsImageWithinRange_ShouldReturnExpectedResult(
            string rawNotification,
            bool expectedResult,
            double framePaddingRelative
        )
        {
            var identificationEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<GraphQLResponse<IdentificationEventResponse>>(rawNotification);

            var notification = GraphQlNotificationSource.ConvertToNotification(identificationEvent.Data);

            var validationService = new ValidationService(
                Logger.None,
                new CropCoordinatesValidator(Logger.None)
            );

            var streamConfiguration = new StreamConfiguration()
            {
                FramePaddingRelative = framePaddingRelative
            };

            var result = validationService.Validate(notification, streamConfiguration);

            Assert.Equal(expectedResult, result);
        }
    }
}