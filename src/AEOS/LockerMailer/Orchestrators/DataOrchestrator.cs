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
        private LockerAnalytics currentAnalytics = new LockerAnalytics();
        private string AeosIntegrationIdentifierType;

        
        private IList<AeosMember> _AeosAllEmployees = new List<AeosMember>();
        private IList<AeosLockers> _AeosAllLockers = new List<AeosLockers>();
        private IList<AeosLockerGroups> _AeosAllLockerGroups = new List<AeosLockerGroups>();
        private IList<AeosIdentifierType> _AeosAllIdentifierTypes = new List<AeosIdentifierType>();
        private IList<AeosIdentifier> _AeosAllIdentifiers = new List<AeosIdentifier>();

        // Fields for tracking assignment changes
        private Dictionary<long, long?> _previousLockerAssignments = new Dictionary<long, long?>();
        private DateTime? _lastAssignmentCheckTime = null;
        private List<LockerAssignmentChange> _assignmentChanges = new List<LockerAssignmentChange>();

        public DataOrchestrator(
            ILogger logger,
            IConfiguration configuration,
            IAeosDataAdapter aeosDataAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.aeosDataAdapter = aeosDataAdapter ?? throw new ArgumentNullException(nameof(aeosDataAdapter));
        }

        public async Task<IList<AeosIdentifierType>> GetIdentifierTypes()
        {
            return _AeosAllIdentifierTypes;
        }

        public async Task<IList<AeosIdentifier>> GetIdentifiersPerType(long identifierType)
        {
            return await aeosDataAdapter.GetIdentifiersPerType(identifierType);
        }

    }
}