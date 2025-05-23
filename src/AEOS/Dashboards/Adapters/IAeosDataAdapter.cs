using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceReference;


namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public interface IAeosDataAdapter
    {

        Task<IList<AeosLockers>> GetLockers();

    }
}