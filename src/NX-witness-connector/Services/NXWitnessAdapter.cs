using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Web;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    public class NXWitnessAdapter : INXWitnessAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public NXWitnessAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task PushGenericEventAsync(
            DateTime? timestamp = null,
            string source = null,
            string caption = null,
            string cameraRef = null
        )
        {
            var httpClient = this.httpClientFactory.CreateClient();

            httpClient.DefaultRequestHeaders.Add("Authorization", this.GetBasicAuthHeader());

            var hostName = this.configuration.GetValue<string>("NXWitness:HostName");
            var port = this.configuration.GetValue<int>("NXWitness:Port");

            if (string.IsNullOrEmpty(hostName))
            {
                throw new InvalidOperationException("NX Witness Server URL must be configured");
            }

            if (hostName.IndexOf("http://") != 0 && hostName.IndexOf("https://") != 0)
            {
                hostName = $"http://{hostName}";
            }

            var requestUrl = $"{hostName}:{port}/api/createEvent?";

            if (timestamp != null)
            {
                requestUrl += $"timestamp={timestamp:o}&";
            }

            if (source != null)
            {
                requestUrl += $"source={HttpUtility.UrlEncode(source)}&";
            }


            if (caption != null)
            {
                requestUrl += $"caption={HttpUtility.UrlEncode(caption)}&";
            }

            if (cameraRef != null)
            {
                requestUrl += "metadata={\"cameraRefs\":[\"" + cameraRef + "\"]}&";
            }

            var result = await httpClient.GetAsync(requestUrl);
            string resultContent = await result.Content.ReadAsStringAsync();

            this.logger.Debug("Response: {response}", resultContent);
        }

        private string GetBasicAuthHeader()
        {
            var username = this.configuration.GetValue<string>("NXWitness:User");
            var password = this.configuration.GetValue<string>("NXWitness:Password");
            var encoding = Encoding.GetEncoding("ISO-8859-1");

            string encoded = System.Convert.ToBase64String(encoding.GetBytes($"{username}:{password}"));

            return $"Basic {encoded}";
        }
    }
}