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
    }
} 