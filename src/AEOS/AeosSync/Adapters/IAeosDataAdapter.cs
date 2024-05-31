using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceReference;


namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public interface IAeosDataAdapter
    {
        Task<IList<AeosMember>> GetEmployees();
        Task<bool> CreateEmployees(AeosMember member, long badgeIdentifierType, long freefieldDefinitionId);
        Task<bool> UpdateEmployee(AeosMember member, long freefieldDefinitionId);
        Task<bool> EnableBiometryOnEmployee(long memberId, long FreefieldDefinitionId, long badgeIdentifierType);
        Task<AeosMember> GetEmployeeByAeosId(long employeeId);
        Task<bool> RemoveEmployee(AeosMember member, long freefieldDefinitionId);
        Task<bool> RemoveEmployeebyId(long employeeId);
        Task<EmployeeInfoComplete> GetEmployeeId(string localSmartFaceId, long localFreefieldDefId);
        
        Task<long> GetBadgeIdentifierType();
        Task<long> GetFreefieldDefinitionId();
        Task<bool> GetKeepUserStatus(long userId);
        Task<bool> RemoveAssignedLockers(long userId);
        Task<bool> UpdateBiometricStatus(long userId, string biometricStatus);
        Task<bool> UpdateBiometricStatusWithSFMember(SmartFaceMember member, string biometricStatus, SupportingData supportData);
    }
}