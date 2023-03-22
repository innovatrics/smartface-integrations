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

            if (DataSource == null)
            {
                throw new InvalidOperationException("The DataSource is not read.");
            }
            if (DataSource != "AEOS" && DataSource != "SFACE")
            {
                throw new InvalidOperationException($"The DataSource has an illegal value: {DataSource}. Legal values are AEOS and SFACE.");
            }

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
                            if (!AeosExtensions.CompareUsers(FoundAeosMember, SFMember))
                            {

                                this.logger.Debug($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated as there is a difference in data");
                                EmployeesToBeUpdatedAeos.Add(new AeosMember(tempid, FoundAeosMember.SmartFaceId, AeosExtensions.GetFirstName(SFMember.FullName), AeosExtensions.GetLastName(SFMember.FullName)));
                            }
                        }
                        else if (DataSource == "AEOS")
                        {
                            if (!AeosExtensions.CompareUsers(FoundAeosMember, SFMember))
                            {

                                this.logger.Debug($"User {SFMember.FullName} with id {SFMember.Id} needs to be updated as there is a difference in data");
                                var joinedNames = AeosExtensions.JoinNames(FoundAeosMember.FirstName, FoundAeosMember.LastName);
                                EmployeesToBeUpdatedSmartFace.Add(new SmartFaceMember(FoundAeosMember.SmartFaceId, joinedNames, joinedNames));
                            }
                        }
                    }
                    else
                    {
                        if (DataSource == "SFACE")
                        {
                            long tempid = (long)1;
                            this.logger.Debug($"SF Member {SFMember.FullName} with id {SFMember.Id} DOES NOT have a copy in AEOS.");
                            EmployeesToBeAddedAeos.Add(new AeosMember(tempid, SFMember.Id, AeosExtensions.GetFirstName(SFMember.FullName), AeosExtensions.GetLastName(SFMember.FullName)));
                        }
                        else if (DataSource == "AEOS")
                        {
                            this.logger.Information($"SmartFace Member {SFMember.FullName} with id {SFMember.Id} DOES NOT have a copy in AEOS. It will be removed from SmartFace.");
                            EmployeesToBeRemovedSmartFace.Add(new SmartFaceMember(SFMember.Id, SFMember.FullName, SFMember.DisplayName));
                        }
                    }
                }

                foreach (var Member in AeosAllMembers)
                {
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

                            if (Member.SmartFaceId != null && Member.ImageData != null)
                            {
                                this.logger.Information($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} is not present in the SmartFace. It will be added into the SmartFace");
                                EmployeesToBeAddedSmartFace.Add(new SmartFaceMember(Member.SmartFaceId, AeosExtensions.JoinNames(Member.FirstName, Member.LastName), AeosExtensions.JoinNames(Member.FirstName, Member.LastName), Member.ImageData));
                            }
                            else
                            {
                                this.logger.Information($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} does not have SmartFace Id defined as a custom field. This user will not be migrated.");
                            }
                        }
                    }
                }
            }

            if (DataSource == "SFACE")
            {
                this.logger.Debug($"The amount of employees to be added to the AEOS: {EmployeesToBeAddedAeos.Count}");
                int EmployeesToBeAddedFailCountAeos = 0;
                int EmployeesToBeAddedSuccessCountAeos = 0;
                foreach (var member in EmployeesToBeAddedAeos)
                {
                    var returnValue = await aeosDataAdapter.CreateEmployees(member, SupportData.SmartFaceBadgeIdentifierType, SupportData.FreefieldDefinitionId);
                    this.logger.Debug($"User created function {member.SmartFaceId} success?: {returnValue}");
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

                this.logger.Debug($"The amount of employees to be updated in the AEOS: {EmployeesToBeUpdatedAeos.Count}");
                int EmployeesToBeUpdatedFailCountAeos = 0;
                int EmployeesToBeUpdatedSuccessCountAeos = 0;
                foreach (var member in EmployeesToBeUpdatedAeos)
                {
                    var returnValue = await aeosDataAdapter.UpdateEmployee(member, SupportData.FreefieldDefinitionId);
                    this.logger.Debug($"User Updated function {member.SmartFaceId} success?: {returnValue}");
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
                    this.logger.Information($"Creating new users in the AEOS: Successful: {EmployeesToBeUpdatedSuccessCountAeos} Failed: {EmployeesToBeUpdatedFailCountAeos}");
                }

                if (EmployeesToBeUpdatedSuccessCountAeos > 0 || EmployeesToBeUpdatedFailCountAeos > 0)
                {
                    this.logger.Information($"Updating users in the AEOS: Successful: {EmployeesToBeUpdatedSuccessCountAeos} Failed: {EmployeesToBeUpdatedFailCountAeos}");
                }

                this.logger.Debug($"The amount of employees to be removed from the AEOS: {EmployeesToBeRemovedAeos.Count}");
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
                        if(await aeosDataAdapter.GetKeepUserStatus(member.Id) == true)
                        {
                            this.logger.Information($"User {AeosExtensions.JoinNames(member.FirstName,member.LastName)} has KeepUser as true. User will not be removed.");
                        }
                        else
                        {
                            this.logger.Warning($"User {AeosExtensions.JoinNames(member.FirstName,member.LastName)} does not have SmartFaceId value. User will be removed.");

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


            else if (DataSource == "AEOS")
            {
                AeosWatchlistId = await smartFaceDataAdapter.InitializeWatchlist();
                this.logger.Debug($"SmartFace watchlist ID set for storing AEOS data is: {AeosWatchlistId}");
                this.logger.Debug($"The amount of employees to be added to the SFACE: {EmployeesToBeAddedSmartFace.Count}");
                int EmployeesToBeAddedFailCountSmartFace = 0;
                int EmployeesToBeAddedSuccessCountSmartFace = 0;
                foreach (var member in EmployeesToBeAddedSmartFace)
                {
                    var returnValue = await smartFaceDataAdapter.CreateEmployee(member, AeosWatchlistId);
                    this.logger.Information($"User created {member.Id}:{member.FullName} success?: {returnValue}");
                    if (returnValue == true)
                    {
                        EmployeesToBeAddedSuccessCountSmartFace += 1;
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


                this.logger.Information($"The amount of employees to be updated in the SmartFace: {EmployeesToBeUpdatedSmartFace.Count}");
                int EmployeesToBeUpdatedFailCountSmartFace = 0;
                int EmployeesToBeUpdatedSuccessCountSmartFace = 0;
                foreach (var member in EmployeesToBeUpdatedSmartFace)
                {
                    var returnValue = await smartFaceDataAdapter.UpdateEmployee(member);
                    this.logger.Information($"User Updated function {member.Id} success?: {returnValue}");
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

                this.logger.Debug($"The amount of employees to be removed from the SmartFace: {EmployeesToBeRemovedSmartFace.Count}");
                int EmployeesToBeRemovedFailCountSmartFace = 0;
                int EmployeesToBeRemovedSuccessCountSmartFace = 0;
                foreach (var member in EmployeesToBeRemovedSmartFace)
                {
                    this.logger.Information($"SmartFace user to be removed->SupportData.FreefieldDefinitionId {SupportData.FreefieldDefinitionId}, member.Id {member.Id}, member.FullName {member.FullName}, member.DisplayName {member.DisplayName}");
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