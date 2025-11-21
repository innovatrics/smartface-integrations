namespace Kone.Api.Client.Clients.Generated
{
    public partial class Oauth2Client
    {
        public event Action<(HttpRequestMessage, string Url)> OnRequest;
        public event Action<HttpResponseMessage> OnResponse;

        partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
        {
            OnRequest?.Invoke((request, url));
        }

        partial void ProcessResponse(HttpClient client, HttpResponseMessage response)
        {
            OnResponse?.Invoke(response);
        }
    }

    public partial class SelfClient
    {
        public event Action<(HttpRequestMessage, string Url)> OnRequest;
        public event Action<HttpResponseMessage> OnResponse;

        partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
        {
            OnRequest?.Invoke((request, url));
        }

        partial void ProcessResponse(HttpClient client, HttpResponseMessage response)
        {
            OnResponse?.Invoke(response);
        }
    }
}
