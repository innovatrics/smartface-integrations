using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Serilog;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public class DataOrchestrator : IDataOrchestrator
    {
        private readonly ILogger _logger;
        private readonly ISmartFaceDataAdapter _smartFaceDataAdapter;
        private readonly IAeosDataAdapter _aeosDataAdapter;

        private readonly string DataSource;
        private string AeosWatchlistId;
        private string FirstNameOrder;
        private bool NoImageWarningNotification;
        private bool KeepPhotoUpToDate;
        private string AutoBiometryPrefix;

        public DataOrchestrator(
            ILogger logger,
            IConfiguration configuration,
            ISmartFaceDataAdapter smartFaceDataAdapter,
            IAeosDataAdapter aeosDataAdapter
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _smartFaceDataAdapter = smartFaceDataAdapter ?? throw new ArgumentNullException(nameof(smartFaceDataAdapter));
            _aeosDataAdapter = aeosDataAdapter ?? throw new ArgumentNullException(nameof(aeosDataAdapter));

            DataSource = configuration.GetValue<string>("AeosSync:DataSource");
            FirstNameOrder = configuration.GetValue<string>("AeosSync:SmartFace:FirstNameOrder");
            NoImageWarningNotification = configuration.GetValue<bool>("AeosSync:Aeos:NoImageWarningNotification");
            KeepPhotoUpToDate = configuration.GetValue<bool>("AeosSync:Aeos:KeepPhotoUpToDate");
            AutoBiometryPrefix = configuration.GetValue<string>("AeosSync:Aeos:AutoBiometryPrefix");

            if (DataSource == null)
            {
                throw new InvalidOperationException("The DataSource is not read.");
            }
            if (DataSource != "AEOS" && DataSource != "SFACE")
            {
                throw new InvalidOperationException($"The DataSource has an illegal value: {DataSource}. Legal values are AEOS and SFACE.");
            }
            if (FirstNameOrder != "first" && FirstNameOrder != "last")
            {
                throw new InvalidOperationException($"The FirstNameOrder has an illegal value: {FirstNameOrder}. Legal values are first and last.");
            }
            _logger.Debug($"INITIATE: KeepPhotoUpToDate:{KeepPhotoUpToDate}");
            
            if(DataSource != "AEOS")
            {
                _logger.Information("Sync started SmartFace -> Aeos");
            }
            else
            {
                _logger.Information("Sync started Aeos -> SmartFace");
            }
            

        }

        public async Task Synchronize()
        {

            _logger.Debug("Data Orchestrator Initalized");

            SupportingData SupportData = new SupportingData(await _aeosDataAdapter.GetFreefieldDefinitionId(), await _aeosDataAdapter.GetBadgeIdentifierType());
            _logger.Debug($"SupportData.FreefieldDefinitionId: {SupportData.FreefieldDefinitionId}, SupportData.SmartFaceBadgeIdentifierType: {SupportData.SmartFaceBadgeIdentifierType}");

            var SmartFaceAllMembers = await _smartFaceDataAdapter.GetEmployees();

            _logger.Debug("Employees defined in SmartFace");
            foreach (var eachMember in SmartFaceAllMembers)
            {
                _logger.Debug(eachMember.ToString());
            }
            _logger.Debug($"The amount of SmartFace users is {SmartFaceAllMembers.Count}");

            
            var AeosAllMembers = await _aeosDataAdapter.GetEmployees();

            _logger.Debug("Employees defined in Aeos");
            foreach (var eachMember in AeosAllMembers)
            {
                _logger.Debug(eachMember.ToString());
            }
            _logger.Debug($"The amount of AEOS users is {AeosAllMembers.Count}");
            

            List<AeosMember> EmployeesToBeAddedAeos = new List<AeosMember>();
            List<AeosMember> EmployeesToBeRemovedAeos = new List<AeosMember>();
            List<AeosMember> EmployeesToBeUpdatedAeos = new List<AeosMember>();

            List<SmartFaceMember> EmployeesToBeAddedSmartFace = new List<SmartFaceMember>();
            List<SmartFaceMember> EmployeesToBeUpdatedSmartFace = new List<SmartFaceMember>();
            List<SmartFaceMember> EmployeesToBeRemovedSmartFace = new List<SmartFaceMember>();

            if (SmartFaceAllMembers != null & AeosAllMembers != null)
            {
                _logger.Debug("Comparing Lists:");
                foreach (var SFMember in SmartFaceAllMembers)
                {
                    var FoundAeosMember = AeosAllMembers.Where(i => i.SmartFaceId == SFMember.Id).FirstOrDefault();
                    if (FoundAeosMember != null)
                    {
                        if (DataSource == "SFACE")
                        {
                            _logger.Debug($"SF Member {SFMember.FullName} with id {SFMember.Id} HAS a copy in AEOS.");

                            long tempid = (long)1;
                            if (!AeosExtensions.CompareUsers(FoundAeosMember, SFMember, FirstNameOrder))
                            {
                                _logger.Information($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated as there is a difference in data");
                                EmployeesToBeUpdatedAeos.Add(new AeosMember(tempid, FoundAeosMember.SmartFaceId, AeosExtensions.GetFirstName(SFMember.FullName, FirstNameOrder), AeosExtensions.GetLastName(SFMember.FullName, FirstNameOrder)));
                            }

                        }
                        else if (DataSource == "AEOS")
                        {
                            var joinedNames = AeosExtensions.JoinNames(FoundAeosMember.FirstName, FoundAeosMember.LastName, FirstNameOrder);
                            _logger.Debug($"FoundAeosMember.FirstName, FoundAeosMember.LastName\n{FoundAeosMember.FirstName}, {FoundAeosMember.LastName}");
                            if (!AeosExtensions.CompareUsers(FoundAeosMember, SFMember, FirstNameOrder) || !AeosExtensions.CompareUserPhoto(FoundAeosMember, SFMember, FirstNameOrder, KeepPhotoUpToDate))
                            {

                                if (!AeosExtensions.CompareUserPhoto(FoundAeosMember, SFMember, FirstNameOrder, KeepPhotoUpToDate))
                                {

                                    if(FoundAeosMember.ImageData != null)
                                    {
                                        _logger.Information($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated as the image does not match");
                                        _logger.Debug($"SFMember.ImageDataId: {SFMember.ImageDataId}");
                                        EmployeesToBeUpdatedSmartFace.Add(new SmartFaceMember(FoundAeosMember.SmartFaceId, joinedNames, joinedNames, FoundAeosMember.ImageData, AeosExtensions.getImageHash(FoundAeosMember.ImageData), SFMember.ImageDataId));
                                    }
                                    
                                }
                                else
                                {
                                    if(FoundAeosMember.ImageData != null)
                                    {
                                        _logger.Information($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated as there is a difference in user data. Image preserved.");
                                        _logger.Debug($"SFMember.ImageDataId: {SFMember.ImageDataId}");
                                        EmployeesToBeUpdatedSmartFace.Add(new SmartFaceMember(FoundAeosMember.SmartFaceId, joinedNames, joinedNames, FoundAeosMember.ImageData, AeosExtensions.getImageHash(FoundAeosMember.ImageData), SFMember.ImageDataId));
                                    }
                                    else
                                    {
                                        _logger.Information($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated as there is a difference in user data. No image.");
                                        _logger.Debug($"SFMember.ImageDataId: {SFMember.ImageDataId}");
                                        EmployeesToBeUpdatedSmartFace.Add(new SmartFaceMember(FoundAeosMember.SmartFaceId, joinedNames, joinedNames, null, AeosExtensions.getImageHash(null), SFMember.ImageDataId));
                                    }
                                    
                                }
                            }
                            /*
                            else
                            {
                                if (!AeosExtensions.CompareUserPhoto(FoundAeosMember, SFMember, FirstNameOrder, KeepPhotoUpToDate))
                                {
                                    this.logger.Debug($"DEBUG THIS SHOULD NOT HAPPEN");
                                    this.logger.Information($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated the image does not match");
                                    this.logger.Debug($"SFMember.ImageDataId: {SFMember.ImageDataId}");
                                    EmployeesToBeUpdatedSmartFace.Add(new SmartFaceMember(FoundAeosMember.SmartFaceId, joinedNames, joinedNames, FoundAeosMember.ImageData, AeosExtensions.getImageHash(FoundAeosMember.ImageData), SFMember.ImageDataId));
                                }
                            }
                            */
                        }
                    }
                    else
                    {
                        if (DataSource == "SFACE")
                        {
                            long tempid = (long)1;
                            _logger.Debug($"SF Member {SFMember.FullName} with id {SFMember.Id} DOES NOT have a copy in AEOS.");
                            var returnValue = await _aeosDataAdapter.GetEmployeeId(SFMember.Id, SupportData.FreefieldDefinitionId);
                            if (returnValue != null)
                            {
                                _logger.Debug($"User DOES have an ID: {returnValue.EmployeeInfo.Id} in AEOS already. User will not be added.");
                            }
                            else
                            {
                                EmployeesToBeAddedAeos.Add(new AeosMember(tempid, SFMember.Id, AeosExtensions.GetFirstName(SFMember.FullName, FirstNameOrder), AeosExtensions.GetLastName(SFMember.FullName, FirstNameOrder)));
                            }

                        }
                        else if (DataSource == "AEOS")
                        {
                            _logger.Debug($"SmartFace Member {SFMember.FullName} with id {SFMember.Id} DOES NOT have a copy in AEOS. It will be removed from SmartFace.");
                            EmployeesToBeRemovedSmartFace.Add(new SmartFaceMember(SFMember.Id, SFMember.FullName, SFMember.DisplayName, null, null, null));
                        }
                    }
                }

                foreach (var Member in AeosAllMembers)
                {
                    _logger.Debug($"Member: {Member.ToString()}");

                    var FoundSmartFaceMember = SmartFaceAllMembers.Where(i => i.Id == Member.SmartFaceId).FirstOrDefault();

                    if (FoundSmartFaceMember == null)
                    {
                        if (DataSource == "SFACE")
                        {
                            _logger.Debug($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} will be removed.");
                            EmployeesToBeRemovedAeos.Add(new AeosMember(Member.Id, Member.SmartFaceId, Member.FirstName, Member.LastName));
                        }
                        else if (DataSource == "AEOS")
                        {

                            if ((Member.SmartFaceId == null || Member.SmartFaceId == "@NotEnabled") && Member.ImageData != null)
                            {
                                _logger.Debug($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} is being checked for enabling biometry.");
                                _logger.Debug($"SupportData.FreefieldDefinitionId {SupportData.FreefieldDefinitionId} SupportData.SmartFaceBadgeIdentifierType {SupportData.SmartFaceBadgeIdentifierType}.");

                                var returnValue = await _aeosDataAdapter.EnableBiometryOnEmployee(Member.Id, SupportData.FreefieldDefinitionId, SupportData.SmartFaceBadgeIdentifierType);

                                if (returnValue)
                                {
                                    _logger.Debug($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id}> biometry was enabled.");
                                    var employeeResponse = await _aeosDataAdapter.GetEmployeeByAeosId(Member.Id);
                                    if (employeeResponse != null)
                                    {
                                        Member.SmartFaceId = employeeResponse.SmartFaceId;
                                    }
                                }

                            }

                            if ((Member.SmartFaceId != null && Member.SmartFaceId != "@NotEnabled") && Member.ImageData != null)
                            {
                                _logger.Information($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} is not present in the SmartFace. User will be added into the SmartFace");

                                EmployeesToBeAddedSmartFace.Add(new SmartFaceMember(Member.SmartFaceId, AeosExtensions.JoinNames(Member.FirstName, Member.LastName, FirstNameOrder), AeosExtensions.JoinNames(Member.FirstName, Member.LastName, FirstNameOrder), Member.ImageData, AeosExtensions.getImageHash(Member.ImageData), null));
                            }
                            else
                            {
                                if (Member.SmartFaceId == null || Member.SmartFaceId == "@NotEnabled")
                                {
                                    _logger.Warning($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} does not have SmartFace Id defined as a custom field. This user will not be migrated.");
                                }
                                
                                if (Member.ImageData == null)
                                {
                                    if (NoImageWarningNotification)
                                    {
                                        _logger.Warning($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} does not have an user photo. This user will not be migrated.");
                                    }
                                }

                            }
                        }
                    }
                    else
                    {
                        if (DataSource == "AEOS")
                        {
                            if (Member.ImageData == null)
                            {
                                _logger.Warning($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} does not have an image in the AOES. User will be removed from SmartFace");
                                _logger.Debug($"EmployeesToBeUpdatedSmartFace.Count {EmployeesToBeUpdatedSmartFace.Count}");
                                
                                EmployeesToBeRemovedSmartFace.Add(new SmartFaceMember(Member.SmartFaceId, AeosExtensions.JoinNames(Member.FirstName, Member.LastName, FirstNameOrder), AeosExtensions.JoinNames(Member.FirstName, Member.LastName, FirstNameOrder), null, null, null));
                            }
                            else
                            {
                                _logger.Debug($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} IS present in the SmartFace. Image: YES");
                            }
                        }
                    }
                }
            }

            if (DataSource == "SFACE")
            {
                if (EmployeesToBeAddedAeos.Count > 0)
                {
                    _logger.Information($"The amount of employees to be added to the AEOS: {EmployeesToBeAddedAeos.Count}");
                    int EmployeesToBeAddedFailCountAeos = 0;
                    int EmployeesToBeAddedSuccessCountAeos = 0;

                    _logger.Debug("DEBUG: Employees to be Added Aeos");
                    foreach (var item in EmployeesToBeAddedAeos)
                    {
                        _logger.Debug($"{item.FirstName} {item.LastName} - {item.SmartFaceId}");
                    }

                    foreach (var member in EmployeesToBeAddedAeos)
                    {
                        _logger.Debug($"Adding: {member.LastName} {member.FirstName} - {member.SmartFaceId}");
                        var returnValue = await _aeosDataAdapter.CreateEmployees(member, SupportData.SmartFaceBadgeIdentifierType, SupportData.FreefieldDefinitionId);
                        
                        _logger.Debug($"Success: {returnValue}");
                        if (returnValue == true)
                        {
                            EmployeesToBeAddedSuccessCountAeos += 1;
                        }
                        else
                        {
                            EmployeesToBeAddedFailCountAeos += 1;
                        }
                    }
                    if (EmployeesToBeAddedSuccessCountAeos > 0 || EmployeesToBeAddedFailCountAeos > 0)
                    {
                        _logger.Information($"Creating new users in the AEOS: Successful: {EmployeesToBeAddedSuccessCountAeos} Failed: {EmployeesToBeAddedFailCountAeos}");
                    }
                }
                if (EmployeesToBeUpdatedAeos.Count > 0)
                {
                    _logger.Information($"The amount of employees to be updated in the AEOS: {EmployeesToBeUpdatedAeos.Count}");
                    int EmployeesToBeUpdatedFailCountAeos = 0;
                    int EmployeesToBeUpdatedSuccessCountAeos = 0;
                    foreach (var member in EmployeesToBeUpdatedAeos)
                    {
                        var returnValue = await _aeosDataAdapter.UpdateEmployee(member, SupportData.FreefieldDefinitionId);
                        _logger.Debug($"User {member.SmartFaceId} updated, success?: {returnValue}");
                        if (returnValue == true)
                        {
                            EmployeesToBeUpdatedSuccessCountAeos += 1;
                        }
                        else
                        {
                            EmployeesToBeUpdatedFailCountAeos += 1;
                        }
                    }

                    if (EmployeesToBeUpdatedSuccessCountAeos > 0 || EmployeesToBeUpdatedFailCountAeos > 0)
                    {
                        _logger.Information($"Updating users in the AEOS: Successful: {EmployeesToBeUpdatedSuccessCountAeos} Failed: {EmployeesToBeUpdatedFailCountAeos}");
                    }
                }

                if (EmployeesToBeRemovedAeos.Count > 0)
                {
                    _logger.Information($"The amount of employees to be removed from the AEOS: {EmployeesToBeRemovedAeos.Count}");

                    int EmployeesToBeRemovedFailCountAeos = 0;
                    int EmployeesToBeRemovedSuccessCountAeos = 0;
                    foreach (var member in EmployeesToBeRemovedAeos)
                    {
                        _logger.Debug($"test->SupportData.FreefieldDefinitionId {SupportData.FreefieldDefinitionId} member.smartfaceId {member.SmartFaceId}");
                        if (member.SmartFaceId != null)
                        {
                            _logger.Debug("member.SmartFaceId = " + member.SmartFaceId);
                            var returnValue = await _aeosDataAdapter.RemoveEmployee(member, SupportData.FreefieldDefinitionId);

                            if (returnValue == true)
                            {
                                EmployeesToBeRemovedSuccessCountAeos += 1;
                            }
                            else
                            {
                                EmployeesToBeRemovedFailCountAeos += 1;
                            }
                        }
                        else
                        {
                            if (await _aeosDataAdapter.GetKeepUserStatus(member.Id) == true)
                            {
                                _logger.Information($"User {AeosExtensions.JoinNames(member.FirstName, member.LastName, FirstNameOrder)} has KeepUser as true. User will not be removed.");
                            }
                            else
                            {
                                _logger.Warning($"User {AeosExtensions.JoinNames(member.FirstName, member.LastName, FirstNameOrder)} does not have SmartFaceId value. User will be removed.");

                                var returnValue = await _aeosDataAdapter.RemoveEmployeebyId(member.Id);
                                if (returnValue == true)
                                {
                                    EmployeesToBeRemovedSuccessCountAeos += 1;
                                }
                                else
                                {
                                    EmployeesToBeRemovedFailCountAeos += 1;
                                }
                            }
                        }
                    }
                    if (EmployeesToBeRemovedSuccessCountAeos > 0 || EmployeesToBeRemovedFailCountAeos > 0)
                    {
                        _logger.Information($"Removing users in the AEOS: Successful: {EmployeesToBeRemovedSuccessCountAeos} Failed: {EmployeesToBeRemovedFailCountAeos}");
                    }
                }
            }

            else if (DataSource == "AEOS")
            {
                AeosWatchlistId = await _smartFaceDataAdapter.InitializeWatchlist();
                _logger.Debug($"SmartFace watchlist ID set for storing AEOS data is: {AeosWatchlistId}");

                if (EmployeesToBeAddedSmartFace.Count > 0)
                {

                    _logger.Information($"The amount of employees to be added to the SFACE: {EmployeesToBeAddedSmartFace.Count}");
                    int EmployeesToBeAddedFailCountSmartFace = 0;
                    int EmployeesToBeAddedSuccessCountSmartFace = 0;
                    foreach (var member in EmployeesToBeAddedSmartFace)
                    {
                        _logger.Debug($"Adding user:-> {member.Id}, {member.FullName}");
                        var returnValue = await _smartFaceDataAdapter.CreateEmployee(member, AeosWatchlistId, AutoBiometryPrefix);
                        _logger.Debug($"User created {member.Id}:{member.FullName} success?: {returnValue}");

                        if (returnValue == true)
                        {

                            EmployeesToBeAddedSuccessCountSmartFace += 1;

                            var BiometricStatusUpdated = await _aeosDataAdapter.UpdateBiometricStatusWithSFMember(member, "Enabled", SupportData);
                            if(!BiometricStatusUpdated)
                            {
                                _logger.Warning("It was not possible to update biometric status.");
                            }
                        }
                        else
                        {
                            EmployeesToBeAddedFailCountSmartFace += 1;
                        }
                    }
                    if (EmployeesToBeAddedSuccessCountSmartFace > 0 || EmployeesToBeAddedFailCountSmartFace > 0)
                    {
                        _logger.Information($"Creating new users in the SmartFace: Successful: {EmployeesToBeAddedSuccessCountSmartFace} Failed: {EmployeesToBeAddedFailCountSmartFace}");
                    }
                }

                if (EmployeesToBeUpdatedSmartFace.Count > 0)
                {
                    _logger.Information($"The amount of employees to be updated in the SmartFace: {EmployeesToBeUpdatedSmartFace.Count}");
                    int EmployeesToBeUpdatedFailCountSmartFace = 0;
                    int EmployeesToBeUpdatedSuccessCountSmartFace = 0;
                    foreach (var member in EmployeesToBeUpdatedSmartFace)
                    {
                        var returnValue = await _smartFaceDataAdapter.UpdateEmployee(member, KeepPhotoUpToDate);
                        _logger.Information($"User {member.Id} updated, success?: {returnValue}");
                        if (returnValue == true)
                        {
                            EmployeesToBeUpdatedSuccessCountSmartFace += 1;
                        }
                        else
                        {
                            EmployeesToBeUpdatedFailCountSmartFace += 1;
                        }
                    }

                    if (EmployeesToBeUpdatedSuccessCountSmartFace > 0 || EmployeesToBeUpdatedFailCountSmartFace > 0)
                    {
                        _logger.Information($"Updating users in the SmartFace: Successful: {EmployeesToBeUpdatedSuccessCountSmartFace} Failed: {EmployeesToBeUpdatedFailCountSmartFace}");
                    }
                }

                if (EmployeesToBeRemovedSmartFace.Count > 0)
                {
                    _logger.Debug($"The amount of employees to be removed from the SmartFace: {EmployeesToBeRemovedSmartFace.Count}");
                    int EmployeesToBeRemovedFailCountSmartFace = 0;
                    int EmployeesToBeRemovedSuccessCountSmartFace = 0;
                    foreach (var member in EmployeesToBeRemovedSmartFace)
                    {
                        _logger.Debug($"SmartFace user to be removed->SupportData.FreefieldDefinitionId {SupportData.FreefieldDefinitionId}, member.Id {member.Id}, member.FullName {member.FullName}, member.DisplayName {member.DisplayName}");
                        if (member.Id != null)
                        {

                            try
                            {
                                await _smartFaceDataAdapter.RemoveEmployee(member);
                                EmployeesToBeRemovedSuccessCountSmartFace += 1;
                            }
                            catch
                            {

                                EmployeesToBeRemovedFailCountSmartFace += 1;
                            }

                            try
                            {
                                var BiometricStatusUpdated = await _aeosDataAdapter.UpdateBiometricStatusWithSFMember(member, "Disabled", SupportData);
                            }
                            catch(Exception e)
                            {
                                _logger.Error(e,"It was not possible to update biometric status.");
                            }

                        }
                        else
                        {
                            EmployeesToBeRemovedFailCountSmartFace += 1;
                        }
                    }
                    if (EmployeesToBeRemovedSuccessCountSmartFace > 0 || EmployeesToBeRemovedFailCountSmartFace > 0)
                    {
                        _logger.Information($"Removing users in the SmartFace: Successful: {EmployeesToBeRemovedSuccessCountSmartFace} Failed: {EmployeesToBeRemovedFailCountSmartFace}");
                    }
                }
            }
        }
    }
}