using System;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AccessController
{
    public interface IFingeraAdapter
    {
        Task OpenAsync(string stationId, int? userId, DateTime timestamp, long score, string camera = "camera", byte[] image = null);
    }
}