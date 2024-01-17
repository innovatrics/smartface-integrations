using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange;
using Innovatrics.SmartFace.Models.API;
using System.Linq;

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

            var labelParts = labelKey
                                .ToUpper()
                                .Replace('-', '_')
                                .Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                                .Skip(1);

            this.RESOLVER_KEY = string.Join('_', labelParts);
        }

        public async Task<string> ResolveUserAsync(string watchlistMemberId)
        {
            if (watchlistMemberId == null)
            {
                throw new ArgumentNullException(nameof(watchlistMemberId));
            }

            this.logger.Information("Resolving {watchlistMemberId}", watchlistMemberId);

            var apiSchema = this.configuration.GetValue<string>("API:Schema");
            var apiHost = this.configuration.GetValue<string>("API:Host");
            var apiPort = this.configuration.GetValue<int?>("API:Port");            

            this.logger.Information("API configured to {schema}://{host}:{port}", apiSchema, apiHost, apiPort);

            var httpClient = this.httpClientFactory.CreateClient();

            var requestUri = $"{apiSchema}://{apiHost}:{apiPort}/api/v1/WatchlistMembers/{watchlistMemberId}";

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

            var httpRequest = await httpClient.SendAsync(httpRequestMessage);

            httpRequest.EnsureSuccessStatusCode();

            var httpRequestStringContent = await httpRequest.Content.ReadAsStringAsync();

            var watchlistMember = Newtonsoft.Json.JsonConvert.DeserializeObject<WatchlistMember>(httpRequestStringContent);

            var cardId = watchlistMember.Labels?
                                            .Where(w => w.Key.ToUpper() == this.RESOLVER_KEY)
                                            .Select(s => s.Value)
                                            .SingleOrDefault();

            return cardId;
        }
    }
}