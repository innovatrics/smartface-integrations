using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.FingeraAdapter
{
    public class FingeraAdapter : IFingeraAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public FingeraAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(string stationId, int? userId, DateTime timestamp, long score, string type = "camera", byte[] image = null)
        {
            var httpClient = this.httpClientFactory.CreateClient();

            var fingeraServer = this.configuration.GetValue<string>("Fingera:Server");

            if (string.IsNullOrEmpty(fingeraServer))
            {
                throw new InvalidOperationException("Fingera server must be configured");
            }

            var requestUrl = $"{fingeraServer}/scserver?type={type}&score={score}&station_id={stationId}&user_id={userId}&timestamp={timestamp:o}";

            HttpContent content;

            if (image != null)
            {
                content = new ByteArrayContent(image);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            }
            else
            {
                content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            }

            var result = await httpClient.PostAsync(requestUrl, content);
            string resultContent = await result.Content.ReadAsStringAsync();

            this.logger.Debug("Response: {response}", resultContent);
        }
    }
}