using Innovatrics.SmartFace.Integrations.AccessController.Resolvers;

namespace Innovatrics.SmartFace.Integrations.FingeraAdapter.Factories
{
    public interface IUserResolverFactory
    {
        IUserResolver Create(string type);
    }
}
