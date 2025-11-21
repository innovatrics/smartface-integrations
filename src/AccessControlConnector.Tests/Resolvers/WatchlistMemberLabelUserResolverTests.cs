using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Xunit;
using NSubstitute;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Tests.Resolvers
{
    public class WatchlistMemberLabelUserResolverTests
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public WatchlistMemberLabelUserResolverTests()
        {
            _logger = Substitute.For<ILogger>();
            _configuration = Substitute.For<IConfiguration>();
            _httpClientFactory = Substitute.For<IHttpClientFactory>();
        }

        [Theory]
        [InlineData("LABEL_INTEGRITY_FACE_TOKEN", "INTEGRITY_FACE_TOKEN", "F123456", "F123456")]
        [InlineData("LABEL_INTEGRITY_FACE_TOKEN", "Integrity_Face_Token", "F123456", "F123456")]
        [InlineData("LABEL_Integrity_Face_Token", "Integrity_Face_Token", "F123456", "F123456")]
        public async Task ResolveUserAsync_ExtractsValue_WhenLabelKeyMatches(
            string configuredLabelKey,
            string notificationKey,
            string notificationValue,
            string expectedValue
        )
        {
            // Arrange
            var resolver = new WatchlistMemberLabelUserResolver(
                _logger,
                _configuration,
                _httpClientFactory,
                configuredLabelKey
            );

            var notification = new GrantedNotification
            {
                WatchlistMemberId = "test-member-id",
                WatchlistMemberDisplayName = "Test Member"
            };

            // Set WatchlistMemberLabels using reflection since it has internal setter
            var labels = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>(notificationKey, notificationValue)
            };
            
            typeof(GrantedNotification)
                .GetProperty(nameof(GrantedNotification.WatchlistMemberLabels))
                .SetValue(notification, labels);

            // Act
            var result = await resolver.ResolveUserAsync(notification);

            // Assert
            Assert.Equal(expectedValue, result);
        }
    }
}

