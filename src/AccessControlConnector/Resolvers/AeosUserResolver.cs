using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Linq;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using System.ComponentModel;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers
{
    public class AeosUserResolver : IUserResolver
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        private readonly string _labelName;
        private string _returnedValue;
        private string _watchlistMemberId;
        private string _aeoslabel;

        public AeosUserResolver(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            string labelKey
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _labelName = _configuration.GetValue<string>("AeosConfiguration:LabelName");
            if(string.IsNullOrEmpty(_labelName)){
                _logger.Error("AeosConfiguration:LabelName is not set in the configuration");
                throw new ArgumentException("AeosConfiguration:LabelName is not set in the configuration");
            }
            _logger.Information("AeosUserResolver: {labelName}", _labelName);

        }

        public async Task<string> ResolveUserAsync(GrantedNotification notification)
        {
            _returnedValue = null;
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            _logger.Information("Resolving {watchlistMemberId} ({watchlistMemberName})", notification.WatchlistMemberId, notification.WatchlistMemberDisplayName);

            if (notification.WatchlistMemberLabels != null)
            {
                _aeoslabel = notification.WatchlistMemberLabels?
                                        .Where(w => w.Key.ToUpper() == _labelName)
                                        .Select(s => s.Value)
                                        .SingleOrDefault();
            }

            if(_aeoslabel != null){
                _logger.Information("AEOS Label {aeoslabel} is available and will be used as clientId", _aeoslabel);
                _returnedValue = _aeoslabel;
            }else{
                _logger.Information("AEOS Label {aeoslabel} is not available, we will check for WatchlistMemberId", _aeoslabel);
                if(notification.WatchlistMemberId != null){
                    _logger.Information("WatchlistMemberId {notification.WatchlistMemberId} is available and will be used as clientId", notification.WatchlistMemberId);
                    _returnedValue = notification.WatchlistMemberId;
                }else{
                    _logger.Error("No valid client ID found - both values for AEOS Label and WatchlistMemberId are either null or empty");
                    throw new ArgumentException("No valid client ID found - both values for Label and WatchlistMemberId are either null or empty");
                }
            }

            return _returnedValue;
        }

    }
}