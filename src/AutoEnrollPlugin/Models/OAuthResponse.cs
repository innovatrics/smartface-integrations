using System;
using Newtonsoft.Json;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models
{
    public class OAuthResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }
    }
}
