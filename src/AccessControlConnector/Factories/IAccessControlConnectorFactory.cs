using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Factories
{
    public interface IAccessControlConnectorFactory
    {
        IAccessControlConnector Create(string accessConnectorType);
    }
}