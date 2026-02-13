using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    /// <summary>
    /// Provides REST API endpoints for locker analytics data.
    /// </summary>
    [Route("api/lockeranalytics")]
    [ApiController]
    public class LockerAnalyticsApiController : ControllerBase
    {
        private readonly IDataOrchestrator dataOrchestrator;
        private readonly IConfiguration configuration;
        private readonly IAeosDataAdapter aeosDataAdapter;

        public LockerAnalyticsApiController(IDataOrchestrator dataOrchestrator, IConfiguration configuration, IAeosDataAdapter aeosDataAdapter)
        {
            this.dataOrchestrator = dataOrchestrator;
            this.configuration = configuration;
            this.aeosDataAdapter = aeosDataAdapter;
        }

        /// <summary>
        /// Returns all locker groups and their statistics.
        /// </summary>
        /// <returns>List of locker groups with statistics.</returns>
        [HttpGet("groups")]
        public async Task<IActionResult> GetGroups()
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            return Ok(analytics.Groups);
        }

         /// <summary>
        /// Returns information for a specific locker group by group ID.
        /// </summary>
        /// <param name="groupId">The ID of the locker group.</param>
        /// <returns>Locker group information, or 404 if not found.</returns>
        /// <response code="200">Returns the locker group data.</response>
        /// <response code="404">If no locker group is found with the given ID.</response>
        [HttpGet("groups/{groupId}")]
        [ProducesResponseType(typeof(LockerGroupAnalytics), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupById(long groupId)
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            var group = analytics.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null)
                return NotFound(new { message = $"Locker group with ID {groupId} not found." });
            return Ok(group);
        }

        /// <summary>
        /// Returns the global top 10 least recently used lockers.
        /// </summary>
        /// <returns>List of least recently used lockers.</returns>
        /// <response code="200">Returns the list of least recently used lockers.</response>
        [HttpGet("leastused")]
        [ProducesResponseType(typeof(IList<LockerInfo>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLeastUsed()
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            return Ok(analytics.GlobalLeastUsedLockers);
        }

        /// <summary>
        /// Returns a summary of all lockers (total, assigned, unassigned, etc.).
        /// </summary>
        /// <returns>Summary statistics for all lockers.</returns>
        /// <response code="200">Returns the summary statistics.</response>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSummary()
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            var summary = new {
                TotalGroups = analytics.Groups.Count,
                TotalLockers = analytics.Groups.Sum(g => g.TotalLockers),
                TotalAssigned = analytics.Groups.Sum(g => g.AssignedLockers),
                TotalUnassigned = analytics.Groups.Sum(g => g.UnassignedLockers)
            };
            return Ok(summary);
        }

        /// <summary>
        /// Returns a list of all employees.
        /// </summary>
        /// <returns>List of employees.</returns>
        /// <response code="200">Returns the list of employees.</response>
        [HttpGet("employees")]
        [ProducesResponseType(typeof(IList<AeosMember>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEmployees()
        {
            var employees = await dataOrchestrator.GetEmployees();
            return Ok(employees);
        }

        /// <summary>
        /// Returns an employee identified by the identifier.
        /// </summary>
        /// <param name="identifier">The Identifier of the employee. Chosen type is configured in the AEOS integration settings.</param>
        /// <returns>Employee data.</returns>
        /// <response code="200">Returns the employee data.</response>
        /// <response code="404">If no employee is found with the given identifier.</response>
        [HttpGet("employees/by-identifier/{identifier}")]
        [ProducesResponseType(typeof(AeosMember), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEmployeesByIdentifier(string identifier)
        {
            var employee = await dataOrchestrator.GetEmployeeByIdentifier(identifier);
            
            if (employee == null)
                return NotFound(new { message = $"No employee found with identifier: {identifier}" });

            return Ok(employee);
        }

        /// <summary>
        /// Returns an employee identified by their email address.
        /// </summary>
        /// <param name="email">The email address of the employee.</param>
        /// <returns>Employee data.</returns>
        /// <response code="200">Returns the employee data.</response>
        /// <response code="404">If no employee is found with the given email.</response>
        [HttpGet("employees/by-email/{email}")]
        [ProducesResponseType(typeof(AeosMember), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEmployeeByEmail(string email)
        {
            var employee = await dataOrchestrator.GetEmployeeByEmail(email);
            
            if (employee == null)
                return NotFound(new { message = $"No employee found with email: {email}" });

            return Ok(employee);
        }

        /// <summary>
        /// Returns changes in locker assignments since the last time this endpoint was called.
        /// If this is the first call since application start, no changes will be returned.
        /// </summary>
        /// <returns>List of assignment changes with employee details including email addresses</returns>
        [HttpGet("email-summary/assignment-changes")]
        [ProducesResponseType(typeof(AssignmentChangesResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<AssignmentChangesResponse>> GetAssignmentChanges()
        {
            try
            {
                var changes = await dataOrchestrator.GetAssignmentChanges();
                return Ok(changes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve assignment changes", details = ex.Message });
            }
        }

        /// <summary>
        /// Returns current assignment summary for a specific group, organized by employees.
        /// Only employees with assigned lockers in the specified group are included.
        /// </summary>
        /// <param name="groupId">The ID of the locker group.</param>
        /// <returns>Current assignment summary organized by employees with their assigned lockers.</returns>
        /// <response code="200">Returns the current assignment summary for the group.</response>
        /// <response code="404">If no locker group is found with the given ID.</response>
        [HttpGet("email-summary/current-assignment/{groupId}")]
        [ProducesResponseType(typeof(GroupAssignmentEmailSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<GroupAssignmentEmailSummary>> GetCurrentAssignmentEmailSummary(long groupId)
        {
            try
            {
                var summary = await dataOrchestrator.GetGroupAssignmentEmailSummary(groupId);
                if (summary == null)
                {
                    return NotFound(new { message = $"Locker group with ID {groupId} not found." });
                }
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve current assignment summary", details = ex.Message });
            }
        }

        /// <summary>
        /// Assigns a locker to an employee.
        /// This endpoint is only available when AeosDashboards:AllowChanges is set to true.
        /// </summary>
        /// <param name="request">The assignment request containing employee ID, locker ID, and optionally NetworkId and PresetId.</param>
        /// <returns>Result of the assignment operation.</returns>
        /// <response code="200">Assignment operation completed successfully.</response>
        /// <response code="403">Changes are not allowed. Set AeosDashboards:AllowChanges to true.</response>
        /// <response code="400">Invalid request parameters or missing required data.</response>
        /// <response code="404">Locker or employee not found, or locker group data not available.</response>
        /// <response code="500">Error occurred during locker assignment.</response>
        [HttpPost("asign-locker")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AssignLocker([FromBody] AssignLockerRequest request)
        {
            // Log the request body for debugging
            if (request != null)
            {
                var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                Log.Information("AssignLocker request body: {RequestBody}", requestJson);
            }
            else
            {
                Log.Warning("AssignLocker called with null request body");
            }

            var allowChanges = configuration.GetValue<bool>("AeosDashboards:AllowChanges", false);
            if (!allowChanges)
            {
                return StatusCode(403, new { error = "Changes are not allowed. Set AeosDashboards:AllowChanges to true to enable this endpoint." });
            }

            // Validate request
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required." });
            }

            if (request.LockerId <= 0)
            {
                return BadRequest(new { error = "Invalid locker ID. Locker ID must be greater than 0." });
            }

            if (request.EmployeeId <= 0)
            {
                return BadRequest(new { error = "Invalid employee ID. Employee ID must be greater than 0." });
            }

            try
            {
                // Get current analytics to find locker group information
                var analytics = await dataOrchestrator.GetLockerAnalytics();
                
                // Find the locker in the analytics
                var lockerInfo = analytics.Groups
                    .SelectMany(g => g.AllLockers)
                    .FirstOrDefault(l => l.Id == request.LockerId);

                if (lockerInfo == null)
                {
                    return NotFound(new { error = $"Locker with ID {request.LockerId} not found." });
                }

                // Find the locker group that contains this locker
                var lockerGroup = analytics.Groups
                    .FirstOrDefault(g => g.AllLockers.Any(l => l.Id == request.LockerId));

                if (lockerGroup == null)
                {
                    return NotFound(new { error = $"Locker group for locker {request.LockerId} not found." });
                }

                // Determine NetworkId and PresetId
                int networkId;
                long presetId;

                if (request.LockerAuthorisationGroupNetworkId.HasValue && request.LockerAuthorisationPresetId.HasValue)
                {
                    // Use provided values
                    networkId = request.LockerAuthorisationGroupNetworkId.Value;
                    presetId = request.LockerAuthorisationPresetId.Value;
                }
                else if (lockerGroup.LockerAuthorisationGroupNetworkId.HasValue && lockerGroup.LockerAuthorisationPresetId.HasValue)
                {
                    // Use values from locker group
                    networkId = lockerGroup.LockerAuthorisationGroupNetworkId.Value;
                    presetId = lockerGroup.LockerAuthorisationPresetId.Value;
                }
                else
                {
                    return BadRequest(new { 
                        error = "Locker Authorisation Group Network ID and Preset ID are required but not available.",
                        details = "Either provide LockerAuthorisationGroupNetworkId and LockerAuthorisationPresetId in the request, or ensure the locker group has these values configured."
                    });
                }

                // Attempt to assign the locker
                var result = await aeosDataAdapter.AssignLocker(
                    request.LockerId,
                    request.EmployeeId,
                    networkId,
                    presetId
                );

                if (result)
                {
                    // Force data refresh after successful assignment
                    await dataOrchestrator.GetLockersData();
                    
                    return Ok(new { 
                        success = true,
                        message = $"Locker {request.LockerId} assigned successfully to employee {request.EmployeeId}.",
                        lockerId = request.LockerId,
                        employeeId = request.EmployeeId,
                        networkId = networkId,
                        presetId = presetId
                    });
                }
                else
                {
                    return StatusCode(500, new { 
                        error = "Failed to assign locker.",
                        message = "The assignment operation did not complete successfully. The locker may already be assigned or the operation may have failed."
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Failed to assign locker",
                    details = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Releases a locker by its ID.
        /// This endpoint is only available when AeosDashboards:AllowChanges is set to true.
        /// </summary>
        /// <param name="lockerId">The ID of the locker to release.</param>
        /// <returns>Result of the release operation.</returns>
        /// <response code="200">Locker release operation completed successfully.</response>
        /// <response code="403">Changes are not allowed. Set AeosDashboards:AllowChanges to true.</response>
        /// <response code="400">Invalid locker ID.</response>
        /// <response code="500">Error occurred during locker release.</response>
        [HttpPost("release-locker/{lockerId}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReleaseLocker(long lockerId)
        {
            var allowChanges = configuration.GetValue<bool>("AeosDashboards:AllowChanges", false);
            if (!allowChanges)
            {
                return StatusCode(403, new { error = "Changes are not allowed. Set AeosDashboards:AllowChanges to true to enable this endpoint." });
            }

            if (lockerId <= 0)
            {
                return BadRequest(new { error = "Invalid locker ID. Locker ID must be greater than 0." });
            }

            try
            {
                var result = await aeosDataAdapter.ReleaseLocker(lockerId);
                if (result)
                {
                    // Force data refresh after successful unassignment
                    await dataOrchestrator.GetLockersData();
                }
                return Ok(new { success = result, message = result ? $"Locker {lockerId} released successfully." : $"Failed to release locker {lockerId}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to release locker", details = ex.Message });
            }
        }

        /// <summary>
        /// Forces a refresh of locker data from AEOS.
        /// </summary>
        /// <returns>Result of the refresh operation.</returns>
        /// <response code="200">Data refresh completed successfully.</response>
        /// <response code="500">Error occurred during data refresh.</response>
        [HttpPost("refresh-data")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RefreshData()
        {
            try
            {
                await dataOrchestrator.GetLockersData();
                return Ok(new { success = true, message = "Locker data refreshed successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to refresh locker data", details = ex.Message });
            }
        }

        /// <summary>
        /// Unlocks a locker by its ID.
        /// This endpoint is only available when AeosDashboards:AllowChanges is set to true.
        /// </summary>
        /// <param name="lockerId">The ID of the locker to unlock.</param>
        /// <returns>Result of the unlock operation.</returns>
        /// <response code="200">Locker unlock operation completed successfully.</response>
        /// <response code="403">Changes are not allowed. Set AeosDashboards:AllowChanges to true.</response>
        /// <response code="400">Invalid locker ID.</response>
        /// <response code="500">Error occurred during locker unlock.</response>
        [HttpPost("unlock-locker/{lockerId}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UnlockLocker(long lockerId)
        {
            var allowChanges = configuration.GetValue<bool>("AeosDashboards:AllowChanges", false);
            if (!allowChanges)
            {
                return StatusCode(403, new { error = "Changes are not allowed. Set AeosDashboards:AllowChanges to true to enable this endpoint." });
            }

            if (lockerId <= 0)
            {
                return BadRequest(new { error = "Invalid locker ID. Locker ID must be greater than 0." });
            }

            try
            {
                var result = await aeosDataAdapter.UnlockLocker(lockerId);
                
                if (result)
                {
                    return Ok(new { 
                        success = true, 
                        message = $"Locker {lockerId} unlocked successfully." 
                    });
                }
                else
                {
                    return StatusCode(500, new { 
                        error = "Failed to unlock locker.", 
                        message = $"Failed to unlock locker {lockerId}." 
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to unlock locker", details = ex.Message });
            }
        }

        /// <summary>
        /// Returns locker access events from the specified date/time.
        /// </summary>
        /// <param name="dateTime">The date/time to search from in ISO 8601 format (e.g., 2026-01-31T00:00:00Z). If not provided, defaults to today at 00:00:00.</param>
        /// <returns>List of locker access events.</returns>
        /// <response code="200">Returns the list of locker access events.</response>
        /// <response code="400">Invalid date/time format.</response>
        /// <response code="500">Error occurred while retrieving locker access events.</response>
        [HttpGet("accessedLockers")]
        [ProducesResponseType(typeof(IList<LockerAccessEvent>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAccessedLockers([FromQuery] string? dateTime = null)
        {
            DateTime fromDateTime;
            
            if (string.IsNullOrWhiteSpace(dateTime))
            {
                // Default to today at 00:00:00
                fromDateTime = DateTime.Today;
            }
            else
            {
                // Try to parse the provided date/time
                if (!DateTime.TryParse(dateTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out fromDateTime))
                {
                    return BadRequest(new { 
                        error = "Invalid date/time format.", 
                        details = "Please provide a valid ISO 8601 date/time format (e.g., 2026-01-31T00:00:00Z).",
                        providedValue = dateTime
                    });
                }
            }

            try
            {
                var events = await aeosDataAdapter.GetLockerAccessEvents(fromDateTime);
                return Ok(events);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve locker access events");
                return StatusCode(500, new { 
                    error = "Failed to retrieve locker access events", 
                    details = ex.Message 
                });
            }
        }
    }

    /// <summary>
    /// Request model for assigning a locker to an employee.
    /// </summary>
    public class AssignLockerRequest
    {
        /// <summary>
        /// The ID of the employee (CarrierId).
        /// </summary>
        public long EmployeeId { get; set; }

        /// <summary>
        /// The ID of the locker.
        /// </summary>
        public long LockerId { get; set; }

        /// <summary>
        /// The Locker Authorisation Group Network ID. If not provided, will be looked up from the locker group.
        /// </summary>
        public int? LockerAuthorisationGroupNetworkId { get; set; }

        /// <summary>
        /// The Locker Authorisation Preset ID. If not provided, will be looked up from the locker group.
        /// </summary>
        public long? LockerAuthorisationPresetId { get; set; }
    }
} 