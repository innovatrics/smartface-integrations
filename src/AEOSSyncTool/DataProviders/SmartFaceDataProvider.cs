using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public class SmartFaceDataProvider : ISmartFaceDataProvider
    {

        public async Task GetWatchlistMembers(string SmartFaceURL)
        {
            Console.WriteLine("SmartFace Data Provider");

            //this.logger.Information("test");
            //var httpClient = this.httpClientFactory.CreateClient();
            var httpClient = new HttpClient();

            //var AEOSSyncSmartFaceServerUrl = this.configuration.GetValue<string>("AEOSSync:SmartFaceServer");

            /*
            if (string.IsNullOrEmpty(AEOSSyncSmartFaceServerUrl))
            {
                throw new InvalidOperationException("AEOSSync SmartFace Server must be configured");
            }
            */
            
            //var requestUrl = $"{AEOSSyncSmartFaceServerUrl}/check/{ticket_id}/{checkpoint_id}/{chip_id}";
            var requestUrl = SmartFaceURL+"/api/v1/WatchlistMembers";

            var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            
            //var result = await httpClient.GetAsync(requestUrl, content);
            var result = await httpClient.GetAsync(requestUrl);
            string resultContent = await result.Content.ReadAsStringAsync();

            Console.WriteLine(resultContent);
            
        }


        public bool SetWatchlistMembers()
        {
            return true;
        }

    }

}