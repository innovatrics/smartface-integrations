using Innovatrics.SmartFace.Integrations.FingeraAdapter.Resolvers;

namespace Innovatrics.SmartFace.Integrations.FingeraAdapter.Factories
{
    public interface IUserResolverFactory
    {
        IUserResolver Create(string type);
    }
}
