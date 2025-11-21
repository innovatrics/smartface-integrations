using System.Net.Http.Headers;
using System.Text;
using Kone.Api.Client.Clients.Generated;

namespace Kone.Api.Client.Clients
{
    public class KoneAuthApiClient
    {
        public event Action<(HttpRequestMessage, string Url)>? OnRequest;
        public event Action<HttpResponseMessage>? OnResponse;

        public const string DefaultScope = "application/inventory";

        private readonly HttpClient _httpClientForOAuth = new();
        private readonly Oauth2Client _oauth2Client;

        private readonly HttpClient _httpClientForSelf = new();

        public KoneAuthApiClient(string clientId, string clientSecret)
        {
            ArgumentNullException.ThrowIfNull(clientId);
            ArgumentNullException.ThrowIfNull(clientSecret);

            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            _httpClientForOAuth.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
            _oauth2Client = new Oauth2Client(_httpClientForOAuth);
            _oauth2Client.OnRequest += r => OnRequest?.Invoke(r);
            _oauth2Client.OnResponse += r => OnResponse?.Invoke(r);
        }

        public Task<AccessTokenResponse> GetDefaultAccessTokenAsync(CancellationToken cancellationToken)
        {
            return GetAccessTokenAsync(DefaultScope, cancellationToken);
        }

        public Task<AccessTokenResponse> GetCallGivingAccessTokenAsync(string buildingId, string groupId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(buildingId);
            ArgumentNullException.ThrowIfNull(groupId);

            var scope = $"application/inventory callgiving/group:{buildingId}:{groupId}";
            return GetAccessTokenAsync(scope, cancellationToken);
        }

        public async Task<ResourceListResponse> GetResourcesAsync(string accessToken, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(accessToken);

            _httpClientForSelf.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var selfClient = new SelfClient(_httpClientForSelf);
            selfClient.OnRequest += SelfClientOnOnRequest;
            selfClient.OnResponse += SelfClientOnOnResponse;

            void SelfClientOnOnRequest((HttpRequestMessage, string Url) obj)
            {
                OnRequest?.Invoke(obj);
            }

            void SelfClientOnOnResponse(HttpResponseMessage obj)
            {
                OnResponse?.Invoke(obj);
            }

            try
            {
                var resources = await selfClient.ResourcesGetAsync(cancellationToken);
                return resources;
            }
            finally
            {
                selfClient.OnRequest -= SelfClientOnOnRequest;
                selfClient.OnResponse -= SelfClientOnOnResponse;
            }
        }

        private async Task<AccessTokenResponse> GetAccessTokenAsync(string scope, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scope);

            var tokenResponse = await _oauth2Client.TokenAsync(new GetAccessToken_request
            {
                Grant_type = GetAccessToken_requestGrant_type.Client_credentials,
                Scope = scope
            }, cancellationToken);

            return tokenResponse;
        }
    }
}
