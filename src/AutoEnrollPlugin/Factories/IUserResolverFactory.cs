using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Resolvers;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Factories
{
    public interface IUserResolverFactory
    {
        IUserResolver Create(string type);
    }
}