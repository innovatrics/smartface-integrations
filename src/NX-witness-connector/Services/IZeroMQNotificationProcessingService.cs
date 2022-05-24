using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    public interface IZeroMQNotificationProcessingService
    {
        Task ProcessNotificationAsync(string topic, string json);
    }
}