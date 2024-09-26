using System;
using Newtonsoft.Json;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models
{
    public class OAuthToken
    {
        public string AccessToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
