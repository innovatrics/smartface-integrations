using Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories
{
    public interface IUserResolverFactory
    {
        IUserResolver Create(string type);
    }
}