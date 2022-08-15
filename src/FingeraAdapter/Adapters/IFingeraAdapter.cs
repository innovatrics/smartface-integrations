using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.FingeraAdapter
{
    public interface IFingeraAdapter
    {
        Task OpenAsync(string stationId, int? userId, DateTime timestamp, long score, string camera = "camera", byte[] image = null);
    }
}