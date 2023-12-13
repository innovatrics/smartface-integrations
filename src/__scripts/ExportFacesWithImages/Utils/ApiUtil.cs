using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages
{
    public static class ApiUtil
    {
        public static async Task<byte[]> GetImageAsync(string url, Guid imageId)
        {
            using (var client = new HttpClient())
            {
                url = SanitizeUrl(url) + $"api/v1/images/{imageId}";

                // Send an HTTP GET request to the image URL
                var response = await client.GetAsync(url);

                // Check if the request was successful (status code 200)
                if (response.IsSuccessStatusCode)
                {
                    // Read the binary content of the response
                    return await response.Content.ReadAsByteArrayAsync();
                }
                else
                {
                    throw new Exception($"Failed to download image. Status code: {response.StatusCode}");
                }
            }
        }

        public static string SanitizeUrl(string url)
        {
            var uri = new Uri(url);
            return uri.ToString();
        }
    }
}
