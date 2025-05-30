using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

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
        [HttpGet("groups/{groupId}")]
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
        [HttpGet("leastused")]
        public async Task<IActionResult> GetLeastUsed()
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            return Ok(analytics.GlobalLeastUsedLockers);
        }

        /// <summary>
        /// Returns a summary of all lockers (total, assigned, unassigned, etc.).
        /// </summary>
        /// <returns>Summary statistics for all lockers.</returns>
        [HttpGet("summary")]
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
        [HttpGet("employees")]
        public async Task<IActionResult> GetEmployees()
        {
            var employees = await dataOrchestrator.GetEmployees();
            return Ok(employees);
        }

        /// <summary>
        /// Returns an employee identified by the SmartFace identifier.
        /// </summary>
        /// <returns>Employee data.</returns>
        [HttpGet("employees/{identifier}")]
        public async Task<IActionResult> GetEmployeesByIdentifier(string identifier)
        {
            //var employees = await dataOrchestrator.GetEmployees();
            var identifierTypes = await dataOrchestrator.GetEmployeesByIdentifier(identifier);
                
//            var employee = identifierTypes.FirstOrDefault(e => e.Id == identifier);

            //var employee = employees.FirstOrDefault(employee => employee.Id == 1);
            //var employee = employees.FirstOrDefault(e => e.Id == identifier);
           // if (employee == null)
           //     return NotFound(new { message = $"Employee with identifier {identifier} not found." });
            return Ok(identifierTypes);
        }
       
    }
} 