using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public interface IOAuthService
    {
        bool IsEnabled { get; }

        Task<string> GetTokenAsync();
    }
}