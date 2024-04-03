using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Connectors;

namespace Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Factories
{
    public interface IMyQConnectorFactory
    {
        MyQConnector Create(string type);
    }
}