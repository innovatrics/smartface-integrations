using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.FingeraAdapter
{
    public interface IFingeraAdapter
    {
        Task OpenAsync(string stationId, string userId, DateTime timestamp, long score, string type = "camera", byte[] image = null);
    }
}