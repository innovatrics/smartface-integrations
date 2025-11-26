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
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly UserResolverFactory _userResolverFactory;

        public UserResolverFactoryTests()
        {
            _logger = Substitute.For<ILogger>();
            _configuration = Substitute.For<IConfiguration>();
            _httpClientFactory = Substitute.For<IHttpClientFactory>();

            _userResolverFactory = new UserResolverFactory(
                _logger,
                _configuration,
                _httpClientFactory
            );
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
            var result = _userResolverFactory.NormalizeType(configurationType);

            Assert.Equal(expectedNormalizedType, result);
        }

        [Theory]
        [InlineData("LABEL_2N_USER_ID", "2N_USER_ID")]
        [InlineData("LABEL_ACCESS_CARD_ID", "ACCESS_CARD_ID")]
        [InlineData("LABEL_XXX", "XXX")]
        [InlineData("LABEL_Integriti_Face_Token", "INTEGRITI_FACE_TOKEN")]
        [InlineData("WATCHLIST_MEMBER_LABEL_2N_USER_ID", "2N_USER_ID")]
        [InlineData("WATCHLIST_MEMBER_LABEL_ACCESS_CARD_ID", "ACCESS_CARD_ID")]
        [InlineData("WATCHLIST_MEMBER_LABEL_XXX", "XXX")]
        public void CreateWatchlistMemberLabelUserResolver(
            string configurationType,
            string expectedLabelKey
        )
        {
            var result = _userResolverFactory.Create(configurationType) as WatchlistMemberLabelUserResolver;

            Assert.NotNull(result);
            Assert.Equal(typeof(WatchlistMemberLabelUserResolver), result.GetType());
            Assert.Equal(expectedLabelKey, result.NormalizedLabelKey);
        }
    }
}