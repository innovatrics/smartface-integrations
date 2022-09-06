using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.RelayConnector.Connectors;

namespace Innovatrics.SmartFace.Integrations.RelayConnector.Factories
{
    public interface IRelayConnectorFactory
    {
        IRelayConnector Create(string type);
    }
}