using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.MyQConnector.Connectors;

namespace Innovatrics.SmartFace.Integrations.MyQConnector.Factories
{
    public interface IMyQConnectorFactory
    {
        Connectors.MyQConnector Create(string type);
    }
}