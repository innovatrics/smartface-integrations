using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public class LockerManagementController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IDataOrchestrator dataOrchestrator;

        public LockerManagementController(IConfiguration configuration, IDataOrchestrator dataOrchestrator)
        {
            this.configuration = configuration;
            this.dataOrchestrator = dataOrchestrator;
        }

        public async Task<IActionResult> Index()
        {
            var isEnabled = configuration.GetValue<bool>("AeosDashboards:LockerManagement:Enabled", false);
            if (!isEnabled)
            {
                return NotFound();
            }

            // Get actual locker groups from the data
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            var actualGroupNames = analytics.Groups.Select(g => g.Name).ToList();

            // Get configured groups and filter to only those that exist in the actual data
            var groupConfigurations = configuration.GetSection("AeosDashboards:LockerManagement:GroupConfiguration")
                .Get<List<LockerManagementGroupConfiguration>>() ?? new List<LockerManagementGroupConfiguration>();

            var validGroups = groupConfigurations
                .Where(g => actualGroupNames.Contains(g.GroupName))
                .ToList();

            // Create a list to track which groups have custom layouts
            var groupsWithCustomLayout = validGroups
                .Where(g => g.GroupLayout != null && g.GroupLayout.Count > 0)
                .Select(g => g.GroupName)
                .ToList();

            ViewBag.Groups = validGroups.Select(g => g.GroupName).ToList();
            ViewBag.GroupsWithCustomLayout = groupsWithCustomLayout;
            
            var groupConfigDict = validGroups.ToDictionary(g => g.GroupName, g => g);
            ViewBag.GroupConfigurations = groupConfigDict;
            ViewBag.LockerAnalytics = analytics;
            ViewBag.WebRefreshPeriodMs = configuration.GetValue<int>("AeosDashboards:Web:WebRefreshPeriodMs", 10000);

            return View();
        }
    }
}

