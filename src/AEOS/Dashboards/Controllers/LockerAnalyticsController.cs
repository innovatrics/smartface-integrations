using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    [Route("[controller]")]
    public class LockerAnalyticsController : Controller
    {
        private readonly IDataOrchestrator dataOrchestrator;

        public LockerAnalyticsController(IDataOrchestrator dataOrchestrator)
        {
            this.dataOrchestrator = dataOrchestrator;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            return View(analytics);
        }
    }
} 