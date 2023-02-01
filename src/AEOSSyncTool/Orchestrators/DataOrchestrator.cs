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
using AEOSSyncTool;

namespace Innovatrics.SmartFace.Integrations.AEOSSync
{
    public class DataOrchestrator : IDataOrchestrator
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly ISmartFaceDataAdapter smartFaceDataAdapter;
        private readonly IAeosDataAdapter aeosDataAdapter;

        private readonly SmartFaceGraphQLClient graphQlClient;       

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
        }

        public async Task Synchronize()
        {

            this.logger.Debug("Data Orchestrator Initalized");            

            // ###
            //  0.
            // ### Get all the supporting data at the beginning of the cycle so it is fresh
            SupportingData SupportData = new SupportingData(await aeosDataAdapter.getFreefieldDefinitionId(),await aeosDataAdapter.getBadgeIdentifierType());
            //this.logger.Information($"Freefield: {SupportData.FreefieldDefinitionId}, Badge: {SupportData.SmartFaceBadgeIdentifierType}" );

            // ###
            //  1.
            // ### Get Data from SmartFace
            
            var SmartFaceAllMembers = await this.smartFaceDataAdapter.getEmployees();

            this.logger.Debug("Employees defined in SmartFace");
            foreach (var eachMember in SmartFaceAllMembers)
            {
                this.logger.Debug(eachMember.ReadMember());
            } 
            this.logger.Debug($"The amount of SmartFace users is {SmartFaceAllMembers.Count}");    
           
           /*
            // REST API Read All the WatchlistMembers, do it per pages for the case there are too many members.
            
            while(allMembers == false)
            {

                
                // lets try it with graphQL instead

                var httpClient = new HttpClient();
                var requestUrl = SmartFaceURL+"/api/v1/WatchlistMembers"+"?PageNumber="+SmartFacePageNumber+"&PageSize="+SmartFacePageSize;
                var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                var result = await httpClient.GetAsync(requestUrl);
                string resultContent = await result.Content.ReadAsStringAsync();

                //Console.WriteLine(resultContent);

                dynamic restResults = JsonConvert.DeserializeObject(resultContent);

                //Console.WriteLine((stuff.items).Count);
                //Console.WriteLine(stuff.items[0].fullName);
                

                // add members from the rest api call into List<member> SmartFaceAllMembers
                foreach (var person in restResults.items)
                {
                    //Console.WriteLine(person);
                    //Console.WriteLine($"Member: \t{person.id}\t{person.fullName}\t{person.displayName}");
                    SmartFaceAllMembers.Add(new SmartFaceMember((string)person.id,(string)person.fullName,(string)person.displayName));
                }

                // check if more iterations are needed
                if((restResults.items).Count == SmartFacePageSize)
                {
                    // lets do it again with new page and merge data from previous and current run together

                    SmartFacePageNumber += 1;
                    //Console.WriteLine("### NEW PAGE");

                }
                else
                {
                    allMembers = true;

                }
            }
            */          

            // ###
            //  2.
            // ### Get Data from AEOS       

            var AeosAllMembers = await this.aeosDataAdapter.getEmployees();
            
            this.logger.Debug("Employees defined in Aeos");
            foreach (var eachMember in AeosAllMembers)
            {
                this.logger.Debug(eachMember.ReadMember());
            }
            this.logger.Debug($"The amount of AEOS users is {AeosAllMembers.Count}");

            // ###
            //  3.
            // ### Compare the list of users between Aeos and SmartFace

            List<AeosMember> EmployeesToBeAdded = new List<AeosMember>();
            List<AeosMember> EmployeesToBeRemoved = new List<AeosMember>();
            List<AeosMember> EmployeesToBeUpdated = new List<AeosMember>();

            if(SmartFaceAllMembers != null & AeosAllMembers != null)
            {
                this.logger.Debug("Comparing Lists:");
                foreach (var SFMember in SmartFaceAllMembers)
                {   
                    var FoundAeosMember = AeosAllMembers.Where(i => i.SmartFaceId == SFMember.Id).FirstOrDefault();
                    if(FoundAeosMember != null)
                    {
                        this.logger.Debug($"SF Member {SFMember.fullName} with id {SFMember.Id} HAS a copy in AEOS.");

                        long tempid = (long)1;
                        if(!AeosExtensions.CompareUsers(FoundAeosMember,SFMember))
                        {
                            // this needs to be tested
                            this.logger.Debug($"User {SFMember.fullName} with id {SFMember.Id} needs to be updated as there is a difference in data");
                            EmployeesToBeUpdated.Add(new AeosMember(tempid,FoundAeosMember.SmartFaceId, AeosExtensions.getFirstName(SFMember.fullName), AeosExtensions.getLastName(SFMember.fullName)));
                        }
                    }
                    else
                    {
                        long tempid = (long)1;
                        this.logger.Debug($"SF Member {SFMember.fullName} with id {SFMember.Id} DOES NOT have a copy in AEOS.");
                        EmployeesToBeAdded.Add(new AeosMember(tempid,SFMember.Id,AeosExtensions.getFirstName(SFMember.fullName), AeosExtensions.getLastName(SFMember.fullName)));                                                
                    }
                }

                foreach (var Member in AeosAllMembers)
                {
                    if(!(SmartFaceAllMembers.Where(i => i.Id == Member.SmartFaceId).Select(i => i.Id).FirstOrDefault() != null))
                    {
                        this.logger.Debug($"Aeos Member {Member.FirstName} {Member.LastName} with id {Member.Id} and SmartFaceId {Member.SmartFaceId} will be removed.");
                        EmployeesToBeRemoved.Add(new AeosMember(Member.Id,Member.SmartFaceId,Member.FirstName,Member.LastName));   
                    }
                }
            }


            this.logger.Debug($"The amount of employees to be added to the AEOS: {EmployeesToBeAdded.Count}");
            int EmployeesToBeAddedFailCount = 0;
            int EmployeesToBeAddedSuccessCount = 0;
            foreach (var member in EmployeesToBeAdded)
            {
                var returnValue = await aeosDataAdapter.createEmployees(member, SupportData.SmartFaceBadgeIdentifierType, SupportData.FreefieldDefinitionId);
                this.logger.Debug($"User created function {member.SmartFaceId} success?: {returnValue}");
                if(returnValue == true)
                {
                    EmployeesToBeAddedSuccessCount += 1;
                }
                else
                {
                    EmployeesToBeAddedFailCount += 1;
                }
            }
            if(EmployeesToBeAddedSuccessCount > 0 || EmployeesToBeAddedFailCount > 0)
            {
                this.logger.Information($"Creating new users in the AEOS:\tSuccessful: {EmployeesToBeAddedSuccessCount}\tFailed: {EmployeesToBeAddedFailCount}");
            }


            this.logger.Debug($"The amount of employees to be updated in the AEOS: {EmployeesToBeUpdated.Count}");
            int EmployeesToBeUpdatedFailCount = 0;
            int EmployeesToBeUpdatedSuccessCount = 0;
            foreach (var member in EmployeesToBeUpdated)
            {
                var returnValue = await aeosDataAdapter.updateEmployee(member,SupportData.FreefieldDefinitionId);
                this.logger.Debug($"User Updated function {member.SmartFaceId} success?: {returnValue}");
                if(returnValue == true)
                {
                    EmployeesToBeUpdatedSuccessCount += 1;
                }
                else
                {
                    EmployeesToBeUpdatedFailCount += 1;
                }
            }
            if(EmployeesToBeUpdatedSuccessCount > 0 || EmployeesToBeUpdatedFailCount > 0)
            {
                this.logger.Information($"Creating new users in the AEOS:\tSuccessful: {EmployeesToBeUpdatedSuccessCount}\tFailed: {EmployeesToBeUpdatedFailCount}");
            }

            if(EmployeesToBeUpdatedSuccessCount > 0 || EmployeesToBeUpdatedFailCount > 0)
            {
                this.logger.Information($"Updating users in the AEOS:\tSuccessful: {EmployeesToBeUpdatedSuccessCount}\tFailed: {EmployeesToBeUpdatedFailCount}");
            }

            this.logger.Debug($"The amount of employees to be removed from the AEOS: {EmployeesToBeRemoved.Count}");

            int EmployeesToBeRemovedFailCount = 0;
            int EmployeesToBeRemovedSuccessCount = 0;
            foreach (var member in EmployeesToBeRemoved)
            {
                this.logger.Debug($"test->SupportData.FreefieldDefinitionId {SupportData.FreefieldDefinitionId} member.smartfaceId {member.SmartFaceId}");
                if(member.SmartFaceId != null)
                {
                    var returnValue = await aeosDataAdapter.removeEmployee(member,SupportData.FreefieldDefinitionId);
                
                    if(returnValue == true)
                    {
                        EmployeesToBeRemovedSuccessCount += 1;
                    }
                    else
                    {
                        EmployeesToBeRemovedFailCount += 1;
                    }
                }
                else
                {
                    EmployeesToBeRemovedFailCount += 1;
                }
            }
            if(EmployeesToBeRemovedSuccessCount > 0 || EmployeesToBeRemovedFailCount > 0)
            {
                this.logger.Information($"Removing users in the AEOS:\tSuccessful: {EmployeesToBeRemovedSuccessCount}\tFailed: {EmployeesToBeRemovedFailCount}");
            }
        }
    }
}