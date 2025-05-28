using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public class LockerAnalyticsController : Controller
    {
        private readonly IDataOrchestrator dataOrchestrator;

        public LockerAnalyticsController(IDataOrchestrator dataOrchestrator)
        {
            this.dataOrchestrator = dataOrchestrator;
        }

        public async Task<IActionResult> Index()
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            return View(analytics);
        }
    }
} 