using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AOESConnector.Connectors;

namespace Innovatrics.SmartFace.Integrations.AOESConnector.Factories
{
    public interface IAOESConnectorFactory
    {
        IAOESConnector Create(string type);
    }
}