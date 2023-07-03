using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using AeosSync;
using System.IO;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public class DataOrchestrator : IDataOrchestrator
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly ISmartFaceDataAdapter smartFaceDataAdapter;
        private readonly IAeosDataAdapter aeosDataAdapter;

        private readonly SmartFaceGraphQLClient graphQlClient;
        private readonly string DataSource;
        private string AeosWatchlistId;
        private string FirstNameOrder;
        private bool NoImageWarningNotification;
        private bool KeepPhotoUpToDate;

        public DataOrchestrator(
            ILogger logger,
            IConfiguration configuration,
            SmartFaceGraphQLClient graphQlClient,
            ISmartFaceDataAdapter smartFaceDataAdapter,
            IAeosDataAdapter aeosDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.smartFaceDataAdapter = smartFaceDataAdapter ?? throw new ArgumentNullException(nameof(smartFaceDataAdapter));
            this.graphQlClient = graphQlClient ?? throw new ArgumentNullException(nameof(graphQlClient));
            this.aeosDataAdapter = aeosDataAdapter ?? throw new ArgumentNullException(nameof(aeosDataAdapter));

            DataSource = configuration.GetValue<string>("AeosSync:DataSource");
            FirstNameOrder = configuration.GetValue<string>("AeosSync:SmartFace:FirstNameOrder");
            NoImageWarningNotification = configuration.GetValue<bool>("AeosSync:Aeos:NoImageWarningNotification");
            KeepPhotoUpToDate = configuration.GetValue<bool>("AeosSync:Aeos:KeepPhotoUpToDate");

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
            this.logger.Debug($"INITIATE: KeepPhotoUpToDate:{KeepPhotoUpToDate}");

        }

        public async Task Synchronize()
        {

            this.logger.Debug("Data Orchestrator Initalized");

            SupportingData SupportData = new SupportingData(await aeosDataAdapter.GetFreefieldDefinitionId(), await aeosDataAdapter.GetBadgeIdentifierType());
            this.logger.Debug($"SupportData.FreefieldDefinitionId: {SupportData.FreefieldDefinitionId}, SupportData.SmartFaceBadgeIdentifierType: {SupportData.SmartFaceBadgeIdentifierType}");

            var SmartFaceAllMembers = await this.smartFaceDataAdapter.GetEmployees();

            this.logger.Debug("Employees defined in SmartFace");
            foreach (var eachMember in SmartFaceAllMembers)
            {
                this.logger.Debug(eachMember.ToString());
            }
            this.logger.Debug($"The amount of SmartFace users is {SmartFaceAllMembers.Count}");

            var AeosAllMembers = await this.aeosDataAdapter.GetEmployees();

            this.logger.Debug("Employees defined in Aeos");
            foreach (var eachMember in AeosAllMembers)
            {
                this.logger.Debug(eachMember.ToString());
            }
            this.logger.Debug($"The amount of AEOS users is {AeosAllMembers.Count}");

            List<AeosMember> EmployeesToBeAddedAeos = new List<AeosMember>();
            List<AeosMember> EmployeesToBeRemovedAeos = new List<AeosMember>();
            List<AeosMember> EmployeesToBeUpdatedAeos = new List<AeosMember>();

            List<SmartFaceMember> EmployeesToBeAddedSmartFace = new List<SmartFaceMember>();
            List<SmartFaceMember> EmployeesToBeUpdatedSmartFace = new List<SmartFaceMember>();
            List<SmartFaceMember> EmployeesToBeRemovedSmartFace = new List<SmartFaceMember>();

            if (SmartFaceAllMembers != null & AeosAllMembers != null)
            {
                this.logger.Debug("Comparing Lists:");
                foreach (var SFMember in SmartFaceAllMembers)
                {
                    var FoundAeosMember = AeosAllMembers.Where(i => i.SmartFaceId == SFMember.Id).FirstOrDefault();
                    if (FoundAeosMember != null)
                    {
                        if (DataSource == "SFACE")
                        {
                            this.logger.Debug($"SF Member {SFMember.FullName} with id {SFMember.Id} HAS a copy in AEOS.");

                            long tempid = (long)1;
                            if (!AeosExtensions.CompareUsers(FoundAeosMember, SFMember, FirstNameOrder))
                            {
                                this.logger.Information($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated as there is a difference in data");
                                EmployeesToBeUpdatedAeos.Add(new AeosMember(tempid, FoundAeosMember.SmartFaceId, AeosExtensions.GetFirstName(SFMember.FullName, FirstNameOrder), AeosExtensions.GetLastName(SFMember.FullName, FirstNameOrder)));
                            }
                            
                        }
                        else if (DataSource == "AEOS")
                        {
                            //this.logger.Information($"aeos:\n{FoundAeosMember.ImageData}");
                            //this.logger.Information($"SF\n{SFMember.ImageData}");

                            var joinedNames = AeosExtensions.JoinNames(FoundAeosMember.FirstName, FoundAeosMember.LastName, FirstNameOrder);
                            if (!AeosExtensions.CompareUsers(FoundAeosMember, SFMember, FirstNameOrder) || !AeosExtensions.CompareUserPhoto(FoundAeosMember, SFMember, FirstNameOrder,KeepPhotoUpToDate))
                            {
                                //File.WriteAllBytes(@$"c:\Users\jberes\Documents\work\Study\test\{FoundAeosMember.SmartFaceId}-FoundAeosMember.jpg",FoundAeosMember.ImageData);
                                //File.WriteAllBytes(@$"c:\Users\jberes\Documents\work\Study\test\{SFMember.Id}-SFMember.jpg",SFMember.ImageData);
                                //System.Environment.Exit();

                                if(!AeosExtensions.CompareUserPhoto(FoundAeosMember, SFMember, FirstNameOrder,KeepPhotoUpToDate))
                                {
                                    this.logger.Information($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated the image does not match");
                                    this.logger.Information($"SFMember.ImageDataId: {SFMember.ImageDataId}");
                                    EmployeesToBeUpdatedSmartFace.Add(new SmartFaceMember(FoundAeosMember.SmartFaceId, joinedNames, joinedNames,FoundAeosMember.ImageData,AeosExtensions.getImageHash(FoundAeosMember.ImageData),SFMember.ImageDataId));
                                }
                                else
                                {
                                    this.logger.Information($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated as there is a difference in data");
                                    this.logger.Information($"SFMember.ImageDataId: {SFMember.ImageDataId}");
                                    EmployeesToBeUpdatedSmartFace.Add(new SmartFaceMember(FoundAeosMember.SmartFaceId, joinedNames, joinedNames,null,AeosExtensions.getImageHash(null),SFMember.ImageDataId));
                                }   
                            }
                            else
                            {
                                if(!AeosExtensions.CompareUserPhoto(FoundAeosMember, SFMember, FirstNameOrder,KeepPhotoUpToDate))
                                {
                                    this.logger.Information($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated the image does not match");
                                    this.logger.Information($"SFMember.ImageDataId: {SFMember.ImageDataId}");
                                    EmployeesToBeUpdatedSmartFace.Add(new SmartFaceMember(FoundAeosMember.SmartFaceId, joinedNames, joinedNames,FoundAeosMember.ImageData,AeosExtensions.getImageHash(FoundAeosMember.ImageData),SFMember.ImageDataId));
                                }
                            }
                        }
                    }
                    else
                    {
                        if (DataSource == "SFACE")
                        {
                            long tempid = (long)1;
                            this.logger.Debug($"SF Member {SFMember.FullName} with id {SFMember.Id} DOES NOT have a copy in AEOS.");
                            var returnValue = await aeosDataAdapter.GetEmployeeId(SFMember.Id, SupportData.FreefieldDefinitionId);
                            if (returnValue != null)
                            {
                                this.logger.Debug($"User DOES have an ID: {returnValue.EmployeeInfo.Id} in AEOS already. User will not be added.");
                            }
                            else
                            {
                                EmployeesToBeAddedAeos.Add(new AeosMember(tempid, SFMember.Id, AeosExtensions.GetFirstName(SFMember.FullName, FirstNameOrder), AeosExtensions.GetLastName(SFMember.FullName, FirstNameOrder)));
                            }

                        }
                        else if (DataSource == "AEOS")
                        {
                            this.logger.Debug($"SmartFace Member {SFMember.FullName} with id {SFMember.Id} DOES NOT have a copy in AEOS. It will be removed from SmartFace.");
                            EmployeesToBeRemovedSmartFace.Add(new SmartFaceMember(SFMember.Id, SFMember.FullName, SFMember.DisplayName, null, null,null));
                        }
                    }
                }

                foreach (var Member in AeosAllMembers)
                {
                    this.logger.Debug($"Member: {Member.ToString()}");

                    var FoundSmartFaceMember = SmartFaceAllMembers.Where(i => i.Id == Member.SmartFaceId).FirstOrDefault();

                    if (FoundSmartFaceMember == null)
                    {
                        if (DataSource == "SFACE")
                        {
                            this.logger.Debug($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} will be removed.");
                            EmployeesToBeRemovedAeos.Add(new AeosMember(Member.Id, Member.SmartFaceId, Member.FirstName, Member.LastName));
                        }
                        else if (DataSource == "AEOS")
                        {

                            if ((Member.SmartFaceId == null || Member.SmartFaceId == "@NotEnabled") && Member.ImageData != null)
                            {
                                this.logger.Debug($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} is being checked for enabling biometry.");

                                var returnValue = await this.aeosDataAdapter.EnableBiometryOnEmployee(Member.Id, SupportData.FreefieldDefinitionId, SupportData.SmartFaceBadgeIdentifierType);

                                if (returnValue)
                                {
                                    this.logger.Information($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id}> biometry was enabled.");
                                    var employeeResponse = await this.aeosDataAdapter.GetEmployeeByAeosId(Member.Id);
                                    if (employeeResponse != null)
                                    {
                                        Member.SmartFaceId = employeeResponse.SmartFaceId;
                                    }
                                }

                            }

                            if ((Member.SmartFaceId != null && Member.SmartFaceId != "@NotEnabled") && Member.ImageData != null)
                            {
                                this.logger.Information($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} is not present in the SmartFace. User will be added into the SmartFace");

                                EmployeesToBeAddedSmartFace.Add(new SmartFaceMember(Member.SmartFaceId, AeosExtensions.JoinNames(Member.FirstName, Member.LastName, FirstNameOrder), AeosExtensions.JoinNames(Member.FirstName, Member.LastName, FirstNameOrder), Member.ImageData, AeosExtensions.getImageHash(Member.ImageData),null));
                            }
                            else
                            {
                                if (Member.SmartFaceId == null || Member.SmartFaceId == "@NotEnabled")
                                {
                                    this.logger.Warning($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} does not have SmartFace Id defined as a custom field. This user will not be migrated.");
                                }
                                if (Member.ImageData == null)
                                {
                                    if (NoImageWarningNotification)
                                    {
                                        this.logger.Warning($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} does not have an user photo. This user will not be migrated.");
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
                                this.logger.Warning($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} does not have an image in the AOES. User will be removed from SmartFace");
                                EmployeesToBeRemovedSmartFace.Add(new SmartFaceMember(Member.SmartFaceId, AeosExtensions.JoinNames(Member.FirstName,Member.LastName,FirstNameOrder), AeosExtensions.JoinNames(Member.FirstName,Member.LastName,FirstNameOrder), null, null,null));
                            }
                            else
                            {
                                this.logger.Debug($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} IS present in the SmartFace. Image: YES");
                            }
                        }
                    }
                }
            }

            if (DataSource == "SFACE")
            {
                if (EmployeesToBeAddedAeos.Count > 0)
                {
                    this.logger.Information($"The amount of employees to be added to the AEOS: {EmployeesToBeAddedAeos.Count}");
                    int EmployeesToBeAddedFailCountAeos = 0;
                    int EmployeesToBeAddedSuccessCountAeos = 0;

                    this.logger.Debug("DEBUG: Employees to be Added Aeos");
                    foreach (var item in EmployeesToBeAddedAeos)
                    {
                        this.logger.Debug($"{item.FirstName} {item.LastName} - {item.SmartFaceId}");
                    }

                    foreach (var member in EmployeesToBeAddedAeos)
                    {
                        this.logger.Debug($"Adding: {member.LastName} {member.FirstName} - {member.SmartFaceId}");
                        var returnValue = await aeosDataAdapter.CreateEmployees(member, SupportData.SmartFaceBadgeIdentifierType, SupportData.FreefieldDefinitionId);
                        this.logger.Debug($"Success: {returnValue}");
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
                        this.logger.Information($"Creating new users in the AEOS: Successful: {EmployeesToBeAddedSuccessCountAeos} Failed: {EmployeesToBeAddedFailCountAeos}");
                    }
                }
                if (EmployeesToBeUpdatedAeos.Count > 0)
                {
                    this.logger.Information($"The amount of employees to be updated in the AEOS: {EmployeesToBeUpdatedAeos.Count}");
                    int EmployeesToBeUpdatedFailCountAeos = 0;
                    int EmployeesToBeUpdatedSuccessCountAeos = 0;
                    foreach (var member in EmployeesToBeUpdatedAeos)
                    {
                        var returnValue = await aeosDataAdapter.UpdateEmployee(member, SupportData.FreefieldDefinitionId);
                        this.logger.Debug($"User {member.SmartFaceId} updated, success?: {returnValue}");
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
                        this.logger.Information($"Updating users in the AEOS: Successful: {EmployeesToBeUpdatedSuccessCountAeos} Failed: {EmployeesToBeUpdatedFailCountAeos}");
                    }
                }

                if (EmployeesToBeRemovedAeos.Count > 0)
                {
                    this.logger.Information($"The amount of employees to be removed from the AEOS: {EmployeesToBeRemovedAeos.Count}");

                    int EmployeesToBeRemovedFailCountAeos = 0;
                    int EmployeesToBeRemovedSuccessCountAeos = 0;
                    foreach (var member in EmployeesToBeRemovedAeos)
                    {
                        this.logger.Debug($"test->SupportData.FreefieldDefinitionId {SupportData.FreefieldDefinitionId} member.smartfaceId {member.SmartFaceId}");
                        if (member.SmartFaceId != null)
                        {
                            this.logger.Debug("member.SmartFaceId = " + member.SmartFaceId);
                            var returnValue = await aeosDataAdapter.RemoveEmployee(member, SupportData.FreefieldDefinitionId);

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
                            if (await aeosDataAdapter.GetKeepUserStatus(member.Id) == true)
                            {
                                this.logger.Information($"User {AeosExtensions.JoinNames(member.FirstName, member.LastName, FirstNameOrder)} has KeepUser as true. User will not be removed.");
                            }
                            else
                            {
                                this.logger.Warning($"User {AeosExtensions.JoinNames(member.FirstName, member.LastName, FirstNameOrder)} does not have SmartFaceId value. User will be removed.");

                                var returnValue = await aeosDataAdapter.RemoveEmployeebyId(member.Id);
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
                        this.logger.Information($"Removing users in the AEOS: Successful: {EmployeesToBeRemovedSuccessCountAeos} Failed: {EmployeesToBeRemovedFailCountAeos}");
                    }
                }
            }

            else if (DataSource == "AEOS")
            {
                AeosWatchlistId = await smartFaceDataAdapter.InitializeWatchlist();
                this.logger.Debug($"SmartFace watchlist ID set for storing AEOS data is: {AeosWatchlistId}");

                if (EmployeesToBeAddedSmartFace.Count > 0)
                {

                    this.logger.Information($"The amount of employees to be added to the SFACE: {EmployeesToBeAddedSmartFace.Count}");
                    int EmployeesToBeAddedFailCountSmartFace = 0;
                    int EmployeesToBeAddedSuccessCountSmartFace = 0;
                    foreach (var member in EmployeesToBeAddedSmartFace)
                    {
                        this.logger.Debug($"Adding user:-> {member.Id}, {member.FullName}");
                        var returnValue = await smartFaceDataAdapter.CreateEmployee(member, AeosWatchlistId);
                        this.logger.Debug($"User created {member.Id}:{member.FullName} success?: {returnValue}");
                        
                        if (returnValue == true)
                        {

                            EmployeesToBeAddedSuccessCountSmartFace += 1;

                            var BiometricStatusUpdated = aeosDataAdapter.UpdateBiometricStatusWithSFMember(member,"Enabled",SupportData);
                        }
                        else
                        {
                            EmployeesToBeAddedFailCountSmartFace += 1;
                        }
                    }
                    if (EmployeesToBeAddedSuccessCountSmartFace > 0 || EmployeesToBeAddedFailCountSmartFace > 0)
                    {
                        this.logger.Information($"Creating new users in the SmartFace: Successful: {EmployeesToBeAddedSuccessCountSmartFace} Failed: {EmployeesToBeAddedFailCountSmartFace}");
                    }
                }

                if (EmployeesToBeUpdatedSmartFace.Count > 0)
                {
                    this.logger.Information($"The amount of employees to be updated in the SmartFace: {EmployeesToBeUpdatedSmartFace.Count}");
                    int EmployeesToBeUpdatedFailCountSmartFace = 0;
                    int EmployeesToBeUpdatedSuccessCountSmartFace = 0;
                    foreach (var member in EmployeesToBeUpdatedSmartFace)
                    {
                        var returnValue = await smartFaceDataAdapter.UpdateEmployee(member,KeepPhotoUpToDate);
                        this.logger.Information($"User {member.Id} updated, success?: {returnValue}");
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
                        this.logger.Information($"Updating users in the SmartFace: Successful: {EmployeesToBeUpdatedSuccessCountSmartFace} Failed: {EmployeesToBeUpdatedFailCountSmartFace}");
                    }
                }

                if (EmployeesToBeRemovedSmartFace.Count > 0)
                {
                    this.logger.Debug($"The amount of employees to be removed from the SmartFace: {EmployeesToBeRemovedSmartFace.Count}");
                    int EmployeesToBeRemovedFailCountSmartFace = 0;
                    int EmployeesToBeRemovedSuccessCountSmartFace = 0;
                    foreach (var member in EmployeesToBeRemovedSmartFace)
                    {
                        this.logger.Debug($"SmartFace user to be removed->SupportData.FreefieldDefinitionId {SupportData.FreefieldDefinitionId}, member.Id {member.Id}, member.FullName {member.FullName}, member.DisplayName {member.DisplayName}");
                        if (member.Id != null)
                        {

                            try
                            {
                                await smartFaceDataAdapter.RemoveEmployee(member);
                                EmployeesToBeRemovedSuccessCountSmartFace += 1;
                            }
                            catch
                            {

                                EmployeesToBeRemovedFailCountSmartFace += 1;
                            }
                        }
                        else
                        {
                            EmployeesToBeRemovedFailCountSmartFace += 1;
                        }
                    }
                    if (EmployeesToBeRemovedSuccessCountSmartFace > 0 || EmployeesToBeRemovedFailCountSmartFace > 0)
                    {
                        this.logger.Information($"Removing users in the SmartFace: Successful: {EmployeesToBeRemovedSuccessCountSmartFace} Failed: {EmployeesToBeRemovedFailCountSmartFace}");
                    }
                }
            }
        }
    }
}