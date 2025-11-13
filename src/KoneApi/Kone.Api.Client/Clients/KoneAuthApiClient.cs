using System.Net.Http.Headers;
using System.Text;
using ManagementApi;

namespace Kone.Api.Client.Clients
{
    public class KoneAuthApiClient
    {
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
        }

        public async Task<AccessTokenResponse> GetAccessTokenAsync(string scope = "application/inventory")
        {
            ArgumentNullException.ThrowIfNull(scope);

            var tokenResponse = await _oauth2Client.TokenAsync(new GetAccessToken_request
            {
                Grant_type = GetAccessToken_requestGrant_type.Client_credentials,
                Scope = scope
            });

            return tokenResponse;
        }

        public async Task<ResourceListResponse> GetResourcesAsync(string accessToken)
        {
            ArgumentNullException.ThrowIfNull(accessToken);

            _httpClientForSelf.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var selfClient = new SelfClient(_httpClientForSelf);
            var resources = await selfClient.ResourcesGetAsync(CancellationToken.None);
            return resources;
        }
    }
}
