using System.Net.Http;
using Microsoft.Extensions.Configuration;

using Serilog;
using Xunit;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories;
using Serilog.Core;
using NSubstitute;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Tests.Factories
{
    public class UserResolverFactoryTests
    {
        public UserResolverFactoryTests()
        {

        }

        [Theory]
        [InlineData("LABEL_2N_USER_ID", "WATCHLIST_MEMBER_LABEL")]
        [InlineData("LABEL_ACCESS_CARD_ID", "WATCHLIST_MEMBER_LABEL")]
        [InlineData("LABEL_XXX", "WATCHLIST_MEMBER_LABEL")]
        [InlineData("WATCHLIST_MEMBER_LABEL", "WATCHLIST_MEMBER_LABEL")]
        [InlineData("WATCHLIST_MEMBER_LABEL_XXX", "WATCHLIST_MEMBER_LABEL")]
        public void NormalizeType(
            string configurationType,
            string expectedNormalizedType
        )
        {
            var logger = Substitute.For<ILogger>();
            var configuration = Substitute.For<IConfiguration>();
            var httpClientFactory = Substitute.For<IHttpClientFactory>();

            var userResolverFactory = new UserResolverFactory(
                logger,
                configuration,
                httpClientFactory
            );

            var result = userResolverFactory.NormalizeType(configurationType);

            Assert.Equal(expectedNormalizedType, result);
        }

        [Theory]
        [InlineData("LABEL_2N_USER_ID", "2N_USER_ID")]
        [InlineData("LABEL_ACCESS_CARD_ID", "ACCESS_CARD_ID")]
        [InlineData("LABEL_XXX", "XXX")]
        [InlineData("WATCHLIST_MEMBER_LABEL_2N_USER_ID", "2N_USER_ID")]
        [InlineData("WATCHLIST_MEMBER_LABEL_ACCESS_CARD_ID", "ACCESS_CARD_ID")]
        [InlineData("WATCHLIST_MEMBER_LABEL_XXX", "XXX")]
        public void CreateWatchlistMemberLabelUserResolver(
            string configurationType,
            string expectedLabelKey
        )
        {
            var logger = Substitute.For<ILogger>();
            var configuration = Substitute.For<IConfiguration>();
            var httpClientFactory = Substitute.For<IHttpClientFactory>();

            var userResolverFactory = new UserResolverFactory(
                logger,
                configuration,
                httpClientFactory
            );

            var result = userResolverFactory.Create(configurationType) as WatchlistMemberLabelUserResolver;

            Assert.NotNull(result);

            Assert.Equal(typeof(WatchlistMemberLabelUserResolver), result.GetType());

            Assert.Equal(expectedLabelKey, result.NormalizedLabelKey);
        }
    }
}