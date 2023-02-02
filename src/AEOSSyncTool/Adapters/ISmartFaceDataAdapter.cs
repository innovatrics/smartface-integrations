using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public interface ISmartFaceDataAdapter
    {
        Task<IList <SmartFaceMember>> getEmployees();
        Task<bool> createEmployee(SmartFaceMember member);
        Task updateEmployee();
        Task removeEmployee();
    }
}