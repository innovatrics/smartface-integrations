using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public class LockerAnalyticsController : Controller
    {
        private readonly IDataOrchestrator dataOrchestrator;
        private readonly IConfiguration configuration;

        public LockerAnalyticsController(IDataOrchestrator dataOrchestrator, IConfiguration configuration)
        {
            this.dataOrchestrator = dataOrchestrator;
            this.configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            ViewBag.WebRefreshPeriodMs = configuration.GetValue<int>("AeosDashboards:Web:WebRefreshPeriodMs", 10000);
            return View(analytics);
        }

        public async Task<IActionResult> LastUpdated()
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            return Json(new { lastUpdated = analytics.LastUpdated });
        }
    }
} 