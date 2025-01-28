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

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers
{
    public class WatchlistMemberLabelUserResolver : IUserResolver
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly string RESOLVER_KEY;

        public WatchlistMemberLabelUserResolver(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            string labelKey
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            if (string.IsNullOrEmpty(labelKey))
            {
                throw new ArgumentNullException(nameof(labelKey));
            }

            RESOLVER_KEY = NormalizeLabelKey(labelKey);
        }

        public async Task<string> ResolveUserAsync(GrantedNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            logger.Information("Resolving {watchlistMemberId} ({watchlistMemberName})", notification.WatchlistMemberId, notification.WatchlistMemberFullName);

            if (notification.WatchlistMemberLabels != null)
            {
                return notification.WatchlistMemberLabels?
                                        .Where(w => w.Key.ToUpper() == RESOLVER_KEY)
                                        .Select(s => s.Value)
                                        .SingleOrDefault();
            }

            return await FetchLabelFromAPI(notification);
        }
        
        private string NormalizeLabelKey(string labelKey)
        {
            var labelParts = labelKey
                                .ToUpper()
                                .Replace('-', '_')
                                .Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                                .Skip(1);

            return string.Join('_', labelParts);
        }

        private async Task<string> FetchLabelFromAPI(GrantedNotification notification)
        {
            var apiSchema = this.configuration.GetValue<string>("API:Schema", "http");
            var apiHost = this.configuration.GetValue<string>("API:Host", "SFApi");
            var apiPort = this.configuration.GetValue<int?>("API:Port", 80);

            this.logger.Information("API configured to {schema}://{host}:{port}", apiSchema, apiHost, apiPort);

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"{apiSchema}://{apiHost}:{apiPort}/api/v1/WatchlistMembers/{notification.WatchlistMemberId}";

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

            var httpRequest = await httpClient.SendAsync(httpRequestMessage);

            httpRequest.EnsureSuccessStatusCode();

            var httpRequestStringContent = await httpRequest.Content.ReadAsStringAsync();

            var watchlistMember = Newtonsoft.Json.JsonConvert.DeserializeObject<WatchlistMember>(httpRequestStringContent);
            
            return watchlistMember.Labels?
                                    .Where(w => w.Key.ToUpper() == RESOLVER_KEY)
                                    .Select(s => s.Value)
                                    .SingleOrDefault();
        }
    }
}