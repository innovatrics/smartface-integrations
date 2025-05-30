using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Collections.Generic;

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

        public LockerAnalyticsApiController(IDataOrchestrator dataOrchestrator)
        {
            this.dataOrchestrator = dataOrchestrator;
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
        /// Returns an employee identified by the SmartFace identifier.
        /// </summary>
        /// <param name="identifier">The SmartFace identifier (badge number) of the employee.</param>
        /// <returns>Employee data.</returns>
        /// <response code="200">Returns the employee data.</response>
        /// <response code="404">If no employee is found with the given identifier.</response>
        [HttpGet("employees/{identifier}")]
        [ProducesResponseType(typeof(AeosMember), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEmployeesByIdentifier(string identifier)
        {
            var employee = await dataOrchestrator.GetEmployeeByIdentifier(identifier);
            
            if (employee == null)
                return NotFound(new { message = $"No employee found with identifier: {identifier}" });

            return Ok(employee);
        }
       
    }
} 