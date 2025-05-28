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
        
        public DataOrchestrator(
            ILogger logger,
            IConfiguration configuration,
            IAeosDataAdapter aeosDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.aeosDataAdapter = aeosDataAdapter ?? throw new ArgumentNullException(nameof(aeosDataAdapter));
        }

        public async Task<LockerAnalytics> GetLockerAnalytics()
        {
            return currentAnalytics;
        }

        public async Task GetLockersData()
        {
            this.logger.Information("Retrieving lockers data from AEOS.");

            var lockers = await this.aeosDataAdapter.GetLockers();
            var employees = await this.aeosDataAdapter.GetEmployees();
            var lockerGroups = await this.aeosDataAdapter.GetLockerGroups();

            currentAnalytics = new LockerAnalytics
            {
                LastUpdated = DateTime.Now,
                Groups = new List<LockerGroupAnalytics>()
            };

            foreach (var lockerGroup in lockerGroups)
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
                    .Select(lockerId => lockers.FirstOrDefault(l => l.Id == lockerId))
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
                    var assignedEmployee = employees.FirstOrDefault(e => e.Id == locker.AssignedTo);
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
                this.logger.Information($"Locker Group: {lockerGroup.Name} (ID: {lockerGroup.Id})");
                this.logger.Information($"Description: {lockerGroup.Description}");
                this.logger.Information($"Function: {lockerGroup.LockerFunction}");
                this.logger.Information($"Template: {lockerGroup.LockerBehaviourTemplate}");
                
                this.logger.Information($"Locker Assignment Statistics:");
                this.logger.Information($"  - Total Lockers: {groupAnalytics.TotalLockers}");
                this.logger.Information($"  - Assigned Lockers: {groupAnalytics.AssignedLockers} ({groupAnalytics.AssignmentPercentage:F1}%)");
                this.logger.Information($"  - Unassigned Lockers: {groupAnalytics.UnassignedLockers} ({100 - groupAnalytics.AssignmentPercentage:F1}%)");

                foreach (var locker in sortedLockers)
                {
                    var assignedEmployee = employees.FirstOrDefault(e => e.Id == locker.AssignedTo);
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

                    this.logger.Information($"  - Locker {locker.Name} (ID: {locker.Id})" + 
                        (assignedEmployee != null ? 
                            $" - Assigned to: {assignedEmployee.FirstName} {assignedEmployee.LastName} (ID: {assignedEmployee.Id})" : 
                            " - Not assigned") +
                        lastUsedInfo);
                }

                if (groupAnalytics.LeastUsedLockers.Any())
                {
                    this.logger.Information($"Top 10 least recently used lockers in this group:");
                    foreach (var locker in groupAnalytics.LeastUsedLockers)
                    {
                        this.logger.Information($"  - Locker {locker.Name} (ID: {locker.Id})" +
                            (locker.AssignedTo.HasValue ?
                                $" - Assigned to: {locker.AssignedEmployeeName}" :
                                " - Not assigned") +
                            $" - Last used: {locker.LastUsed:yyyy-MM-dd HH:mm:ss} ({locker.DaysSinceLastUse:F1} days ago)");
                    }
                }

                this.logger.Information("----------------------------------------");
            }

            // Calculate global least used lockers
            currentAnalytics.UpdateGlobalLeastUsedLockers();

            // Log global least used lockers
            if (currentAnalytics.GlobalLeastUsedLockers.Any())
            {
                this.logger.Information("Global Top 10 least recently used lockers:");
                foreach (var locker in currentAnalytics.GlobalLeastUsedLockers)
                {
                    this.logger.Information($"  - Locker {locker.Name} (ID: {locker.Id})" +
                        (locker.AssignedTo.HasValue ?
                            $" - Assigned to: {locker.AssignedEmployeeName}" :
                            " - Not assigned") +
                        $" - Last used: {locker.LastUsed:yyyy-MM-dd HH:mm:ss} ({locker.DaysSinceLastUse:F1} days ago)");
                }
            }
        }
    }
}