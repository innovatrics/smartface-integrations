using Microsoft.Extensions.Caching.Memory;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public interface IExclusiveMemoryCache : IMemoryCache
    {
        public object Lock { get; }
    }
}
