using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.DataExportTool
{
    public static class HttpClientExtensions
    {
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        public static async Task<T> GetGenericAsync<T>(this HttpClient client, string url, CancellationToken cancellationToken = default)
        {
            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            return JsonSerializer.Deserialize<T>(content, Opts);
        }

        public static async Task<TResponse> PostGenericAsync<TResponse>(this HttpClient client, string url, object request, CancellationToken cancellationToken = default)
        {
            var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, MediaTypeNames.Application.Json);
            var response = await client.PostAsync(url, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            return JsonSerializer.Deserialize<TResponse>(content, Opts);
        }

        public static async Task PostGenericAsync(this HttpClient client, string url, object request, CancellationToken cancellationToken = default)
        {
            var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, MediaTypeNames.Application.Json);
            var response = await client.PostAsync(url, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public static async Task PutGenericAsync(this HttpClient client, string url, object request, CancellationToken cancellationToken = default)
        {
            var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, MediaTypeNames.Application.Json);
            var response = await client.PutAsync(url, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }
}