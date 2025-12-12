using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceReference;


namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public interface IAeosDataAdapter
    {

        Task<IList<AeosLockers>> GetLockers();
        Task<IList<AeosLockerGroups>> GetLockerGroups();
        Task<IList<ServiceReference.LockerAuthorisationGroupInfo>> GetLockerAuthorisationGroups();
        Task<IList<AeosMember>> GetEmployees();
        Task<IList<AeosIdentifierType>> GetIdentifierTypes();
        Task<IList<AeosIdentifier>> GetIdentifiersPerType(long identifierType);
        Task<IList<AeosMember>> GetEmployeesByIdentifier(string identifier);
        Task<bool> ReleaseLocker(long lockerId);
        Task<IList<ServiceReference.TemplateInfo>> GetTemplates(string unitOfAuthType);
    }
}