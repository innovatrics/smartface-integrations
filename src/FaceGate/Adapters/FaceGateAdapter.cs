using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.FaceGate
{
    public class FaceGateAdapter : IFaceGateAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public FaceGateAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task OpenAsync(string checkpoint_id, string ticket_id, int chip_id = 12)
        {
            var httpClient = this.httpClientFactory.CreateClient();

            var faceGateServerUrl = this.configuration.GetValue<string>("FaceGate:Server");

            if (string.IsNullOrEmpty(faceGateServerUrl))
            {
                throw new InvalidOperationException("FaceGate Server must be configured");
            }
            
            var requestUrl = $"{faceGateServerUrl}/check/{ticket_id}/{checkpoint_id}/{chip_id}";

            var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            
            var result = await httpClient.PostAsync(requestUrl, content);
            string resultContent = await result.Content.ReadAsStringAsync();

            this.logger.Debug("Response: {response}", resultContent);
        }
    }
}