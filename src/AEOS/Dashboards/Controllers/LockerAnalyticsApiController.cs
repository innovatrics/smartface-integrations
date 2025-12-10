using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Configuration;

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
        /// <param name="request">The assignment request containing employee ID and locker ID.</param>
        /// <returns>Result of the assignment operation.</returns>
        /// <response code="200">Assignment operation completed successfully.</response>
        /// <response code="403">Changes are not allowed. Set AeosDashboards:AllowChanges to true.</response>
        /// <response code="400">Invalid request parameters.</response>
        [HttpPost("asign-locker")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AssignLocker([FromBody] AssignLockerRequest request)
        {
            var allowChanges = configuration.GetValue<bool>("AeosDashboards:AllowChanges", false);
            if (!allowChanges)
            {
                return StatusCode(403, new { error = "Changes are not allowed. Set AeosDashboards:AllowChanges to true to enable this endpoint." });
            }

            if (request == null)
            {
                return BadRequest(new { error = "Request body is required." });
            }

            // Dummy implementation - to be updated later
            return Ok(new { message = "Locker assignment endpoint - implementation pending", employeeId = request.EmployeeId, lockerId = request.LockerId });
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
                return Ok(new { success = result, message = result ? $"Locker {lockerId} released successfully." : $"Failed to release locker {lockerId}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to release locker", details = ex.Message });
            }
        }
    }

    /// <summary>
    /// Request model for assigning a locker to an employee.
    /// </summary>
    public class AssignLockerRequest
    {
        /// <summary>
        /// The ID of the employee.
        /// </summary>
        public long EmployeeId { get; set; }

        /// <summary>
        /// The ID of the locker.
        /// </summary>
        public long LockerId { get; set; }
    }
} 