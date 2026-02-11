using Innovatrics.SmartFace.Integrations.AccessController.Resolvers;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories
{
    public interface IUserResolverFactory
    {
        IUserResolver Create(string type);
    }
}