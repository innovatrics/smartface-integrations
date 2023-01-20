using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AEOSConnector.Connectors;

namespace Innovatrics.SmartFace.Integrations.AEOSConnector.Factories
{
    public interface IAEOSConnectorFactory
    {
        IAEOSConnector Create(string type);
    }
}