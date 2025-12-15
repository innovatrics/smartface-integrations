using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    public class LockerManagementGroupConfiguration
    {
        public required string GroupName { get; set; }
        public List<LockerManagementRow>? GroupLayout { get; set; }
    }
}

