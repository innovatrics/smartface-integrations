using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    public interface IBridge
    {
        Task PushGenericEventAsync(
            DateTime? timestamp = null,
            string source = null,
            string caption = null,
            Guid? streamId = null
        );
    }
}