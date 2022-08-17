using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.FaceGate
{
    public interface IFaceGateAdapter
    {
        Task OpenAsync(string checkpoint_id, string ticket_id, int chip_id = 12);
    }
}