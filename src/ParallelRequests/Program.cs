using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace SmartFace.Integrations.IFaceManualCall
{
    public class Program
    {
        private async static Task Main()
        {
            var file = File.ReadAllText(@".\payload.json");

            var tasks = new List<Task>();

            for (var i = 0; i < 100; i++)
            {
                Console.WriteLine($"Creating task {i}");

                var index = i.ToString();
                var task = Task.Run(() => SendRequest(file, index));

                // var task = SendRequest(file, i);

                tasks.Add(task);
            }

            Console.WriteLine($"{tasks.Count} tasks created");

            await Task.WhenAll(tasks.ToArray());

            Console.WriteLine($"Quit");
        }

        private async static Task SendRequest(string file, string index)
        {
            var requestsSentCount = 0;

            var payloadObject = JsonConvert.DeserializeObject<dynamic>(file);

            while (requestsSentCount < 1000)
            {
                var httpClient = new HttpClient();

                // set custom properties
                // payloadObject.id = Guid.NewGuid();

                var payload = new StringContent(JsonConvert.SerializeObject(payloadObject), Encoding.UTF8, "application/json");

                using (var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://noauth-demo.smartfacecloud.com/api/v1/Watchlists/Search"
                ))
                {
                    request.Content = payload;

                    Console.WriteLine($"Sending {index}");

                    var stopwatch = Stopwatch.StartNew();

                    var response = await httpClient.SendAsync(request);

                    stopwatch.Stop();

                    Console.WriteLine($"Sent {index} in {stopwatch.ElapsedMilliseconds}ms, response {response.StatusCode}");
                };

                requestsSentCount++;
            }
        }
    }
}