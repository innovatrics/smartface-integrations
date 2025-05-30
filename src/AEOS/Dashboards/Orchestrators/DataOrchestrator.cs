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
using Innovatrics.SmartFace.Integrations.AEOS.SmartFaceClients;
using System.IO;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public class DataOrchestrator : IDataOrchestrator
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IAeosDataAdapter aeosDataAdapter;
        private LockerAnalytics currentAnalytics = new LockerAnalytics();
        private string AeosIntegrationIdentifierType;

        
        private IList<AeosMember> _AeosAllEmployees = new List<AeosMember>();
        private IList<AeosLockers> _AeosAllLockers = new List<AeosLockers>();
        private IList<AeosLockerGroups> _AeosAllLockerGroups = new List<AeosLockerGroups>();
        private IList<AeosIdentifierType> _AeosAllIdentifierTypes = new List<AeosIdentifierType>();
        private IList<AeosIdentifier> _AeosAllIdentifiers = new List<AeosIdentifier>();

        public DataOrchestrator(
            ILogger logger,
            IConfiguration configuration,
            IAeosDataAdapter aeosDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.aeosDataAdapter = aeosDataAdapter ?? throw new ArgumentNullException(nameof(aeosDataAdapter));

            AeosIntegrationIdentifierType = configuration.GetValue<string>("AeosDashboards:Aeos:Integration:SmartFace:IdentifierType") ?? throw new InvalidOperationException("The AEOS integration identifier type is not read.");
        }

        public async Task<LockerAnalytics> GetLockerAnalytics()
        {
            return currentAnalytics;
        }

        public async Task GetLockersData()
        {
            this.logger.Information("Retrieving lockers data from AEOS.");

            _AeosAllLockers = await this.aeosDataAdapter.GetLockers();
            _AeosAllLockerGroups = await this.aeosDataAdapter.GetLockerGroups();
            _AeosAllIdentifierTypes = await this.aeosDataAdapter.GetIdentifierTypes();
            _AeosAllIdentifiers = (await this.aeosDataAdapter.GetIdentifiersPerType(
                _AeosAllIdentifierTypes.FirstOrDefault(t => t.Name == AeosIntegrationIdentifierType)?.Id ?? 0))
                .Where(e => e.CarrierId != 0)
                .ToList();

            _AeosAllEmployees = await this.aeosDataAdapter.GetEmployees();

            this.logger.Information($"Lockers data retrieved");

            // ---------------------------

            foreach (var identifier in _AeosAllIdentifiers)
            {
                this.logger.Debug($"Identifier: {identifier.Id}, BadgeNumber: {identifier.BadgeNumber}, Blocked: {identifier.Blocked}, CarrierId: {identifier.CarrierId}");
            }

            currentAnalytics = new LockerAnalytics
            {
                LastUpdated = DateTime.Now,
                Groups = new List<LockerGroupAnalytics>()
            };

            foreach (var lockerGroup in _AeosAllLockerGroups)
            {
                var groupAnalytics = new LockerGroupAnalytics
                {
                    Id = lockerGroup.Id,
                    Name = lockerGroup.Name,
                    Description = lockerGroup.Description,
                    Function = lockerGroup.LockerFunction,
                    Template = lockerGroup.LockerBehaviourTemplate
                };

                var sortedLockers = lockerGroup.LockerIds
                    .Select(lockerId => _AeosAllLockers.FirstOrDefault(l => l.Id == lockerId))
                    .Where(l => l != null)
                    .OrderBy(l => l.Name)
                    .ToList();

                // Calculate statistics
                groupAnalytics.TotalLockers = sortedLockers.Count;
                groupAnalytics.AssignedLockers = sortedLockers.Count(l => l.AssignedTo > 0);
                groupAnalytics.UnassignedLockers = sortedLockers.Count(l => l.AssignedTo == 0);
                groupAnalytics.AssignmentPercentage = groupAnalytics.TotalLockers > 0 
                    ? (groupAnalytics.AssignedLockers * 100.0 / groupAnalytics.TotalLockers) 
                    : 0;

                // Process all lockers
                foreach (var locker in sortedLockers)
                {
                    var assignedEmployee = _AeosAllEmployees.FirstOrDefault(e => e.Id == locker.AssignedTo);
                    var lockerInfo = new LockerInfo
                    {
                        Id = locker.Id,
                        Name = locker.Name,
                        LastUsed = locker.LastUsed != DateTime.MinValue ? locker.LastUsed : null,
                        AssignedTo = locker.AssignedTo > 0 ? locker.AssignedTo : null,
                        AssignedEmployeeName = assignedEmployee != null 
                            ? $"{assignedEmployee.FirstName} {assignedEmployee.LastName}" 
                            : null,
                        DaysSinceLastUse = locker.LastUsed != DateTime.MinValue 
                            ? (DateTime.Now - locker.LastUsed).TotalDays 
                            : 0
                    };
                    groupAnalytics.AllLockers.Add(lockerInfo);
                }

                // Get top 10 least used lockers
                groupAnalytics.LeastUsedLockers = groupAnalytics.AllLockers
                    .Where(l => l.LastUsed.HasValue)
                    .OrderByDescending(l => l.DaysSinceLastUse)
                    .Take(10)
                    .ToList();

                currentAnalytics.Groups.Add(groupAnalytics);

                // Log the information as before
                this.logger.Debug($"Locker Group: {lockerGroup.Name} (ID: {lockerGroup.Id})");
                this.logger.Debug($"Description: {lockerGroup.Description}");
                this.logger.Debug($"Function: {lockerGroup.LockerFunction}");
                this.logger.Debug($"Template: {lockerGroup.LockerBehaviourTemplate}");
                
                this.logger.Debug($"Locker Assignment Statistics:");
                this.logger.Debug($"  - Total Lockers: {groupAnalytics.TotalLockers}");
                this.logger.Debug($"  - Assigned Lockers: {groupAnalytics.AssignedLockers} ({groupAnalytics.AssignmentPercentage:F1}%)");
                this.logger.Debug($"  - Unassigned Lockers: {groupAnalytics.UnassignedLockers} ({100 - groupAnalytics.AssignmentPercentage:F1}%)");

                foreach (var locker in sortedLockers)
                {
                    var assignedEmployee = _AeosAllEmployees.FirstOrDefault(e => e.Id == locker.AssignedTo);
                    var lastUsedInfo = "";
                    if (locker.LastUsed != DateTime.MinValue)
                    {
                        var duration = DateTime.Now - locker.LastUsed;
                        lastUsedInfo = $" - Last used: {locker.LastUsed:yyyy-MM-dd HH:mm:ss} ({duration.TotalDays:F1} days ago)";
                    }
                    else
                    {
                        lastUsedInfo = " - Never used";
                    }

                    this.logger.Debug($"  - Locker {locker.Name} (ID: {locker.Id})" + 
                        (assignedEmployee != null ? 
                            $" - Assigned to: {assignedEmployee.FirstName} {assignedEmployee.LastName} (ID: {assignedEmployee.Id})" : 
                            " - Not assigned") +
                        lastUsedInfo);
                }

                if (groupAnalytics.LeastUsedLockers.Any())
                {
                    this.logger.Debug($"Top 10 least recently used lockers in this group:");
                    foreach (var locker in groupAnalytics.LeastUsedLockers)
                    {
                        this.logger.Debug($"  - Locker {locker.Name} (ID: {locker.Id})" +
                            (locker.AssignedTo.HasValue ?
                                $" - Assigned to: {locker.AssignedEmployeeName}" :
                                " - Not assigned") +
                            $" - Last used: {locker.LastUsed:yyyy-MM-dd HH:mm:ss} ({locker.DaysSinceLastUse:F1} days ago)");
                    }
                }

                this.logger.Debug("----------------------------------------");
            }

            // Calculate global least used lockers
            currentAnalytics.UpdateGlobalLeastUsedLockers();

            // Log global least used lockers
            if (currentAnalytics.GlobalLeastUsedLockers.Any())
            {
                this.logger.Debug("Global Top 10 least recently used lockers:");
                foreach (var locker in currentAnalytics.GlobalLeastUsedLockers)
                {
                    this.logger.Debug($"  - Locker {locker.Name} (ID: {locker.Id})" +
                        (locker.AssignedTo.HasValue ?
                            $" - Assigned to: {locker.AssignedEmployeeName}" :
                            " - Not assigned") +
                        $" - Last used: {locker.LastUsed:yyyy-MM-dd HH:mm:ss} ({locker.DaysSinceLastUse:F1} days ago)");
                }
            }
        }

        public async Task<IList<AeosMember>> GetEmployees()
        {
            // Add assigned lockers to each employee using current data
            foreach (var employee in _AeosAllEmployees)
            {
                var assignedLockers = _AeosAllLockers.Where(l => l.AssignedTo == employee.Id).ToList();
                employee.AssignedLockers = assignedLockers;
                this.logger.Debug($"Employee {employee.FirstName} {employee.LastName} (ID: {employee.Id}) has {assignedLockers.Count} assigned lockers");
            }

            return _AeosAllEmployees;
        }

        public async Task<IList<AeosIdentifierType>> GetIdentifierTypes()
        {
            return _AeosAllIdentifierTypes;
        }

        public async Task<IList<AeosIdentifier>> GetIdentifiersPerType(long identifierType)
        {
            return await aeosDataAdapter.GetIdentifiersPerType(identifierType);
        }

        public async Task<AeosMember> GetEmployeeByIdentifier(string identifier)
        {
            this.logger.Information($"Finding employee for identifier: {identifier}");
            
            var matchingIdentifier = _AeosAllIdentifiers.FirstOrDefault(i => i.BadgeNumber == identifier);
            if (matchingIdentifier == null)
            {
                this.logger.Warning($"No identifier found with BadgeNumber: {identifier}");
                return null;
            }

            if (matchingIdentifier.CarrierId == 0)
            {
                this.logger.Warning($"Identifier {identifier} has no associated carrier (CarrierId is 0)");
                return null;
            }

            var employee = _AeosAllEmployees.FirstOrDefault(e => e.Id == matchingIdentifier.CarrierId);
            if (employee == null)
            {
                this.logger.Warning($"No employee found with Id: {matchingIdentifier.CarrierId} for identifier: {identifier}");
                return null;
            }

            // Find all lockers assigned to this employee
            var assignedLockers = _AeosAllLockers.Where(l => l.AssignedTo == employee.Id).ToList();
            employee.AssignedLockers = assignedLockers;

            this.logger.Information($"Found employee: {employee.FirstName} {employee.LastName} (ID: {employee.Id}) for identifier: {identifier} with {assignedLockers.Count} assigned lockers");
            return employee;
        }
    }
}