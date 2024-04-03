using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AEpuConnector.Connectors;

namespace Innovatrics.SmartFace.Integrations.AEpuConnector.Factories
{
    public interface IAEpuConnectorFactory
    {
        IAEpuConnector Create(string type);
    }
}