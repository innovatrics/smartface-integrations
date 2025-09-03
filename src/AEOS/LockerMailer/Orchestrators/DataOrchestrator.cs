using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using Innovatrics.SmartFace.Integrations.LockerMailer;
using System.IO;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class DataOrchestrator : IDataOrchestrator
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IKeilaDataAdapter keilaDataAdapter;
        private readonly ISmtpMailAdapter smtpMailAdapter;
        private readonly IDashboardsDataAdapter dashboardsDataAdapter;

        // Fields for tracking assignment changes
        private Dictionary<long, long?> _previousLockerAssignments = new Dictionary<long, long?>();
        private DateTime? _lastAssignmentCheckTime = null;
        //private List<LockerAssignmentChange> _assignmentChanges = new List<LockerAssignmentChange>();

        public DataOrchestrator(
            ILogger logger,
            IConfiguration configuration,
            IKeilaDataAdapter keilaDataAdapter,
            ISmtpMailAdapter smtpMailAdapter,
            IDashboardsDataAdapter dashboardsDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.keilaDataAdapter = keilaDataAdapter ?? throw new ArgumentNullException(nameof(keilaDataAdapter));
            this.smtpMailAdapter = smtpMailAdapter ?? throw new ArgumentNullException(nameof(smtpMailAdapter));
            this.dashboardsDataAdapter = dashboardsDataAdapter ?? throw new ArgumentNullException(nameof(dashboardsDataAdapter));
        }


    }
}