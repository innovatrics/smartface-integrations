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
        private IList<ServiceReference.LockerAuthorisationGroupInfo> _AeosAllLockerAuthorisationGroups = new List<ServiceReference.LockerAuthorisationGroupInfo>();
        private IList<AeosIdentifierType> _AeosAllIdentifierTypes = new List<AeosIdentifierType>();
        private IList<AeosIdentifier> _AeosAllIdentifiers = new List<AeosIdentifier>();

        // Fields for tracking assignment changes
        private Dictionary<long, long?> _previousLockerAssignments = new Dictionary<long, long?>();
        private DateTime? _lastAssignmentCheckTime = null;
        private List<LockerAssignmentChange> _assignmentChanges = new List<LockerAssignmentChange>();

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

        public Task<LockerAnalytics> GetLockerAnalytics()
        {
            return Task.FromResult(currentAnalytics);
        }

        public Task<AssignmentChangesResponse> GetAssignmentChanges()
        {
            var currentCheckTime = DateTime.Now;
            var changes = new List<LockerAssignmentChange>();

            logger.Information($"GetAssignmentChanges called. Last check time: {_lastAssignmentCheckTime}, Total lockers: {_AeosAllLockers?.Count ?? 0}");

            // If this is the first call, initialize the previous assignments and return empty changes
            if (!_lastAssignmentCheckTime.HasValue)
            {
                logger.Information("First call to GetAssignmentChanges - initializing previous assignments");
                UpdatePreviousAssignments();
                _lastAssignmentCheckTime = currentCheckTime;
                
                return Task.FromResult(new AssignmentChangesResponse
                {
                    LastCheckTime = currentCheckTime,
                    CurrentCheckTime = currentCheckTime,
                    Changes = changes,
                    TotalChanges = 0
                });
            }

            // Compare current assignments with previous assignments
            foreach (var locker in _AeosAllLockers?.Where(l => l != null) ?? Enumerable.Empty<AeosLockers>())
            {
                var previousAssignment = _previousLockerAssignments.ContainsKey(locker.Id) 
                    ? _previousLockerAssignments[locker.Id] 
                    : null;
                
                var currentAssignment = locker.AssignedTo > 0 ? (long?)locker.AssignedTo : null;

                if (previousAssignment != currentAssignment)
                {
                    logger.Information($"Change detected for locker {locker.Id} ({locker.Name}): {previousAssignment} -> {currentAssignment}");
                    // Create separate events for each change
                    var changesForLocker = CreateAssignmentChanges(locker, previousAssignment, currentAssignment);
                    changes.AddRange(changesForLocker);
                }
            }

            logger.Information($"Found {changes.Count} changes in locker assignments");

            // Update the previous assignments for next comparison
            UpdatePreviousAssignments();

            var response = new AssignmentChangesResponse
            {
                LastCheckTime = _lastAssignmentCheckTime.Value,
                CurrentCheckTime = currentCheckTime,
                Changes = changes,
                TotalChanges = changes.Count
            };

            _lastAssignmentCheckTime = currentCheckTime;
            _assignmentChanges.AddRange(changes);

            return Task.FromResult(response);
        }

        private List<LockerAssignmentChange> CreateAssignmentChanges(AeosLockers locker, long? previousAssignment, long? currentAssignment)
        {
            var changes = new List<LockerAssignmentChange>();
            var group = _AeosAllLockerGroups.FirstOrDefault(g => g.LockerIds.Contains(locker.Id));
            var groupName = group?.Name ?? "Unknown Group";

            // If there was a previous assignment and now there isn't, create an "Unassigned" event
            if (previousAssignment.HasValue && !currentAssignment.HasValue)
            {
                var previousEmployee = _AeosAllEmployees.FirstOrDefault(e => e.Id == previousAssignment.Value);
                var previousIdentifier = previousEmployee != null 
                    ? _AeosAllIdentifiers.FirstOrDefault(i => i.CarrierId == previousEmployee.Id)?.BadgeNumber 
                    : null;

                changes.Add(new LockerAssignmentChange
                {
                    LockerId = locker.Id,
                    LockerName = locker.Name,
                    GroupName = groupName,
                    PreviousAssignedTo = previousAssignment,
                    PreviousAssignedEmployeeName = previousEmployee != null 
                        ? $"{previousEmployee.FirstName} {previousEmployee.LastName}" 
                        : null,
                    PreviousAssignedEmployeeIdentifier = previousIdentifier,
                    PreviousAssignedEmployeeEmail = previousEmployee?.Email,
                    NewAssignedTo = null,
                    NewAssignedEmployeeName = null,
                    NewAssignedEmployeeIdentifier = null,
                    NewAssignedEmployeeEmail = null,
                    ChangeTimestamp = DateTime.Now,
                    ChangeType = "Unassigned"
                });
            }

            // If there is a current assignment (regardless of previous), create an "Assigned" event
            if (currentAssignment.HasValue)
            {
                var currentEmployee = _AeosAllEmployees.FirstOrDefault(e => e.Id == currentAssignment.Value);
                var currentIdentifier = currentEmployee != null 
                    ? _AeosAllIdentifiers.FirstOrDefault(i => i.CarrierId == currentEmployee.Id)?.BadgeNumber 
                    : null;

                changes.Add(new LockerAssignmentChange
                {
                    LockerId = locker.Id,
                    LockerName = locker.Name,
                    GroupName = groupName,
                    PreviousAssignedTo = previousAssignment,
                    PreviousAssignedEmployeeName = previousAssignment.HasValue 
                        ? (_AeosAllEmployees.FirstOrDefault(e => e.Id == previousAssignment.Value) is { } prevEmployee
                            ? $"{prevEmployee.FirstName} {prevEmployee.LastName}" 
                            : null)
                        : null,
                    PreviousAssignedEmployeeIdentifier = previousAssignment.HasValue 
                        ? _AeosAllIdentifiers.FirstOrDefault(i => i.CarrierId == previousAssignment.Value)?.BadgeNumber 
                        : null,
                    PreviousAssignedEmployeeEmail = previousAssignment.HasValue 
                        ? _AeosAllEmployees.FirstOrDefault(e => e.Id == previousAssignment.Value)?.Email 
                        : null,
                    NewAssignedTo = currentAssignment,
                    NewAssignedEmployeeName = currentEmployee != null 
                        ? $"{currentEmployee.FirstName} {currentEmployee.LastName}" 
                        : null,
                    NewAssignedEmployeeIdentifier = currentIdentifier,
                    NewAssignedEmployeeEmail = currentEmployee?.Email,
                    ChangeTimestamp = DateTime.Now,
                    ChangeType = "Assigned"
                });
            }

            return changes;
        }

        private void UpdatePreviousAssignments()
        {
            _previousLockerAssignments.Clear();
            foreach (var locker in _AeosAllLockers)
            {
                _previousLockerAssignments[locker.Id] = locker.AssignedTo > 0 ? (long?)locker.AssignedTo : null;
            }
        }

        public async Task GetLockersData()
        {
            this.logger.Debug("Retrieving lockers data from AEOS.");

            _AeosAllLockers = await this.aeosDataAdapter.GetLockers();
            _AeosAllLockerGroups = await this.aeosDataAdapter.GetLockerGroups();
            _AeosAllLockerAuthorisationGroups = await this.aeosDataAdapter.GetLockerAuthorisationGroups();
            _AeosAllIdentifierTypes = await this.aeosDataAdapter.GetIdentifierTypes();
            _AeosAllIdentifiers = (await this.aeosDataAdapter.GetIdentifiersPerType(
                _AeosAllIdentifierTypes.FirstOrDefault(t => t.Name == AeosIntegrationIdentifierType)?.Id ?? 0))
                .Where(e => e.CarrierId != 0)
                .ToList();

            _AeosAllEmployees = await this.aeosDataAdapter.GetEmployees();

            this.logger.Information($"Lockers data retrieved: {_AeosAllLockers.Count} lockers, {_AeosAllLockerGroups.Count} locker groups, {_AeosAllIdentifierTypes.Count} identifier types, {_AeosAllIdentifiers.Count} identifiers, {_AeosAllEmployees.Count} employees");

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
                // Find the LockerAuthorisationGroup that contains this LockerGroup
                var authGroup = _AeosAllLockerAuthorisationGroups?.FirstOrDefault(ag => 
                    ag.LockerGroupIdList != null && 
                    ag.LockerGroupIdList.Contains(lockerGroup.Id));

                var groupAnalytics = new LockerGroupAnalytics
                {
                    Id = lockerGroup.Id,
                    Name = lockerGroup.Name,
                    Description = lockerGroup.Description,
                    Function = lockerGroup.LockerFunction,
                    Template = lockerGroup.LockerBehaviourTemplate,
                    LockerAuthorisationGroupId = authGroup?.Id,
                    AvailablePresets = new List<LockerAuthorisationPresetInfo>()
                };

                var sortedLockers = lockerGroup.LockerIds
                    .Select(lockerId => _AeosAllLockers.FirstOrDefault(l => l.Id == lockerId))
                    .OfType<AeosLockers>()
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
                    var assignedEmployeeIdentifier = assignedEmployee != null 
                        ? _AeosAllIdentifiers.FirstOrDefault(i => i.CarrierId == assignedEmployee.Id)?.BadgeNumber 
                        : null;
                    
                    var lockerInfo = new LockerInfo
                    {
                        Id = locker.Id,
                        Name = locker.Name,
                        LastUsed = locker.LastUsed != DateTime.MinValue ? locker.LastUsed : null,
                        AssignedTo = locker.AssignedTo > 0 ? locker.AssignedTo : null,
                        AssignedEmployeeName = assignedEmployee != null 
                            ? $"{assignedEmployee.FirstName} {assignedEmployee.LastName}" 
                            : null,
                        AssignedEmployeeIdentifier = assignedEmployeeIdentifier,
                        AssignedEmployeeEmail = assignedEmployee?.Email,
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

        public Task<IList<AeosMember>> GetEmployees()
        {
            // Add assigned lockers to each employee using current data
            foreach (var employee in _AeosAllEmployees)
            {
                var assignedLockers = _AeosAllLockers.Where(l => l.AssignedTo == employee.Id).ToList();
                employee.AssignedLockers = assignedLockers;

                // Find the identifier for this employee
                var matchingIdentifier = _AeosAllIdentifiers.FirstOrDefault(i => i.CarrierId == employee.Id);
                if (matchingIdentifier != null)
                {
                    employee.Identifier = matchingIdentifier.BadgeNumber;
                }

                this.logger.Debug($"Employee {employee.FirstName} {employee.LastName} (ID: {employee.Id}) has {assignedLockers.Count} assigned lockers");
            }

            return Task.FromResult<IList<AeosMember>>(_AeosAllEmployees);
        }

        public Task<IList<AeosIdentifierType>> GetIdentifierTypes()
        {
            return Task.FromResult<IList<AeosIdentifierType>>(_AeosAllIdentifierTypes);
        }

        public async Task<IList<AeosIdentifier>> GetIdentifiersPerType(long identifierType)
        {
            return await aeosDataAdapter.GetIdentifiersPerType(identifierType);
        }

        public Task<AeosMember?> GetEmployeeByIdentifier(string identifier)
        {
            this.logger.Information($"Finding employee for identifier: {identifier}");
            
            var matchingIdentifier = _AeosAllIdentifiers.FirstOrDefault(i => i.BadgeNumber == identifier);
            if (matchingIdentifier == null)
            {
                this.logger.Warning($"No identifier found with BadgeNumber: {identifier}");
                return Task.FromResult<AeosMember?>(null);
            }

            if (matchingIdentifier.CarrierId == 0)
            {
                this.logger.Warning($"Identifier {identifier} has no associated carrier (CarrierId is 0)");
                return Task.FromResult<AeosMember?>(null);
            }

            var employee = _AeosAllEmployees.FirstOrDefault(e => e.Id == matchingIdentifier.CarrierId);
            if (employee == null)
            {
                this.logger.Warning($"No employee found with Id: {matchingIdentifier.CarrierId} for identifier: {identifier}");
                return Task.FromResult<AeosMember?>(null);
            }

            // Find all lockers assigned to this employee
            var assignedLockers = _AeosAllLockers.Where(l => l.AssignedTo == employee.Id).ToList();
            employee.AssignedLockers = assignedLockers;
            employee.Identifier = identifier;

            this.logger.Information($"Found employee: {employee.FirstName} {employee.LastName} (ID: {employee.Id}) for identifier: {identifier} with {assignedLockers.Count} assigned lockers");
            return Task.FromResult<AeosMember?>(employee);
        }

        public Task<AeosMember?> GetEmployeeByEmail(string email)
        {
            this.logger.Information($"Finding employee for email: {email}");
            
            var employee = _AeosAllEmployees.FirstOrDefault(e => e.Email == email);
            if (employee == null)
            {
                this.logger.Warning($"No employee found with email: {email}");
                return Task.FromResult<AeosMember?>(null);
            }

            // Find all lockers assigned to this employee
            var assignedLockers = _AeosAllLockers.Where(l => l.AssignedTo == employee.Id).ToList();
            employee.AssignedLockers = assignedLockers;

            // Find the identifier for this employee
            var matchingIdentifier = _AeosAllIdentifiers.FirstOrDefault(i => i.CarrierId == employee.Id);
            if (matchingIdentifier != null)
            {
                employee.Identifier = matchingIdentifier.BadgeNumber;
            }

            this.logger.Information($"Found employee: {employee.FirstName} {employee.LastName} (ID: {employee.Id}) for email: {email} with {assignedLockers.Count} assigned lockers");
            return Task.FromResult<AeosMember?>(employee);
        }

        public Task<GroupAssignmentEmailSummary?> GetGroupAssignmentEmailSummary(long groupId)
        {
            logger.Information($"Getting email summary for group ID: {groupId}");

            // Find the requested group
            var group = _AeosAllLockerGroups.FirstOrDefault(g => g.Id == groupId);
            if (group == null)
            {
                logger.Warning($"Group with ID {groupId} not found");
                return Task.FromResult<GroupAssignmentEmailSummary?>(null);
            }

            // Get all lockers in this group
            var groupLockers = _AeosAllLockers
                .Where(l => group.LockerIds.Contains(l.Id))
                .Where(l => l.AssignedTo > 0) // Only assigned lockers
                .ToList();

            // Group lockers by assigned employee
            var employeeAssignments = new Dictionary<long, EmployeeAssignmentSummary>();

            foreach (var locker in groupLockers)
            {
                var employee = _AeosAllEmployees.FirstOrDefault(e => e.Id == locker.AssignedTo);
                if (employee == null) continue;

                var employeeIdentifier = _AeosAllIdentifiers
                    .FirstOrDefault(i => i.CarrierId == employee.Id)?.BadgeNumber;

                var lockerInfo = new SimplifiedLockerInfo
                {
                    Id = locker.Id,
                    Name = locker.Name,
                    LastUsed = locker.LastUsed != DateTime.MinValue ? locker.LastUsed : null,
                    DaysSinceLastUse = locker.LastUsed != DateTime.MinValue 
                        ? (DateTime.Now - locker.LastUsed).TotalDays 
                        : 0
                };

                if (!employeeAssignments.ContainsKey(employee.Id))
                {
                    employeeAssignments[employee.Id] = new EmployeeAssignmentSummary
                    {
                        EmployeeId = employee.Id,
                        EmployeeName = $"{employee.FirstName} {employee.LastName}",
                        EmployeeEmail = employee.Email,
                        EmployeeIdentifier = employeeIdentifier,
                        AssignedLockers = new List<SimplifiedLockerInfo>(),
                        TotalAssignedLockers = 0
                    };
                }

                employeeAssignments[employee.Id].AssignedLockers.Add(lockerInfo);
                employeeAssignments[employee.Id].TotalAssignedLockers++;
            }

            // Sort employees by name and lockers by name within each employee
            var sortedEmployeeAssignments = employeeAssignments.Values
                .OrderBy(e => e.EmployeeName)
                .ToList();

            foreach (var employeeAssignment in sortedEmployeeAssignments)
            {
                employeeAssignment.AssignedLockers = employeeAssignment.AssignedLockers
                    .OrderBy(l => l.Name)
                    .ToList();
            }

            var summary = new GroupAssignmentEmailSummary
            {
                GroupId = group.Id,
                GroupName = group.Name,
                GroupDescription = group.Description,
                EmployeeAssignments = sortedEmployeeAssignments,
                TotalEmployeesWithAssignments = sortedEmployeeAssignments.Count,
                TotalAssignedLockers = sortedEmployeeAssignments.Sum(e => e.TotalAssignedLockers)
            };

            logger.Information($"Email summary for group {group.Name}: {summary.TotalEmployeesWithAssignments} employees with {summary.TotalAssignedLockers} assigned lockers");
            return Task.FromResult<GroupAssignmentEmailSummary?>(summary);
        }
    }
}