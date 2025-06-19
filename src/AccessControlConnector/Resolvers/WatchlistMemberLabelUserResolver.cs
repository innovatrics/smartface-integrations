using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange;
using Innovatrics.SmartFace.Models.API;
using System.Linq;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers
{
    public class WatchlistMemberLabelUserResolver : IUserResolver
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _normalizedLabelKey;

        public string NormalizedLabelKey => _normalizedLabelKey;

        public WatchlistMemberLabelUserResolver(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            string labelKey
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            _normalizedLabelKey = NormalizeLabelKey(labelKey);
        }

        public async Task<string> ResolveUserAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            _logger.Information("Resolving {watchlistMemberId} ({watchlistMemberName})", notification.WatchlistMemberId, notification.WatchlistMemberDisplayName);

            if (notification.WatchlistMemberLabels != null)
            {
                return notification.WatchlistMemberLabels?
                                        .Where(w => w.Key.ToUpper() == _normalizedLabelKey)
                                        .Select(s => s.Value)
                                        .SingleOrDefault();
            }

            return await FetchLabelFromAPI(notification);
        }
        
        private string NormalizeLabelKey(string labelKey)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(labelKey);

            var normalizedLabelKey = labelKey;

            if (labelKey.StartsWith("LABEL_", StringComparison.OrdinalIgnoreCase))
            {
                normalizedLabelKey = labelKey.Substring("LABEL_".Length);
            }

            if (labelKey.StartsWith($"{UserResolverFactory.WATCHLIST_MEMBER_LABEL_TYPE}_", StringComparison.OrdinalIgnoreCase))
            {
                normalizedLabelKey = labelKey.Substring($"{UserResolverFactory.WATCHLIST_MEMBER_LABEL_TYPE}_".Length);
            }

            var labelParts = normalizedLabelKey
                                .ToUpper()
                                .Replace('-', '_')
                                .Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                                //.Skip(1)
                                .ToArray()
                            ;

            return string.Join('_', labelParts);
        }

        private async Task<string> FetchLabelFromAPI(GrantedNotification notification)
        {
            var apiSchema = _configuration.GetValue<string>("API:Schema", "http");
            var apiHost = _configuration.GetValue<string>("API:Host", "SFApi");
            var apiPort = _configuration.GetValue<int?>("API:Port", 80);

            _logger.Information("API configured to {schema}://{host}:{port}", apiSchema, apiHost, apiPort);

            var httpClient = _httpClientFactory.CreateClient();

            var requestUri = $"{apiSchema}://{apiHost}:{apiPort}/api/v1/WatchlistMembers/{notification.WatchlistMemberId}";

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

            var httpRequest = await httpClient.SendAsync(httpRequestMessage);

            httpRequest.EnsureSuccessStatusCode();

            var httpRequestStringContent = await httpRequest.Content.ReadAsStringAsync();

            var watchlistMember = Newtonsoft.Json.JsonConvert.DeserializeObject<WatchlistMember>(httpRequestStringContent);
            
            return watchlistMember.Labels?
                                    .Where(w => w.Key.ToUpper() == _normalizedLabelKey)
                                    .Select(s => s.Value)
                                    .SingleOrDefault();
        }
    }
}