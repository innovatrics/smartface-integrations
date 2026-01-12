using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Linq;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Resolvers
{
    public class AeosUserResolver : IUserResolver
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        private readonly string _labelNameFaceToken ;
        private readonly string _labelNameQRToken;
        private readonly bool _labelStrictUsageMode;
        private string _returnedValue;
        private string _watchlistMemberId;
        private string _aeoslabelFaceToken;
        private string _aeoslabelQRToken;

        public AeosUserResolver(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            string labelKey
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _labelNameFaceToken = _configuration.GetValue<string>("AeosConfiguration:FaceTokenLabelName");
            _labelNameQRToken = _configuration.GetValue<string>("AeosConfiguration:QRTokenLabelName");
            _labelStrictUsageMode = _configuration.GetValue<bool>("AeosConfiguration:LabelStrictUsageMode", false);
            if (string.IsNullOrEmpty(_labelNameFaceToken) && string.IsNullOrEmpty(_labelNameQRToken) && _labelStrictUsageMode)
            {
                _logger.Error("AeosConfiguration:FaceTokenLabelName and AecosConfiguration:QRTokenLabelName are not set in the configuration and LabelStrictUsageMode is enabled");
                throw new ArgumentException("AeosConfiguration:FaceTokenLabelName and AecosConfiguration:QRTokenLabelName are not set in the configuration and LabelStrictUsageMode is enabled");
            }
            _logger.Information("AeosUserResolver: FaceTokenLabelName: {labelNameFaceToken}", _labelNameFaceToken);
            _logger.Information("AeosUserResolver: QRTokenLabelName: {labelNameQRToken}", _labelNameQRToken);
            _logger.Information("AeosUserResolver: LabelStrictUsageMode: {labelStrictUsageMode}", _labelStrictUsageMode);
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
                _aeoslabelFaceToken = notification.WatchlistMemberLabels?
                                        .Where(w => w.Key.ToUpper() == _labelNameFaceToken)
                                        .Select(s => s.Value)
                                        .SingleOrDefault();
                _aeoslabelQRToken = notification.WatchlistMemberLabels?
                                        .Where(w => w.Key.ToUpper() == _labelNameQRToken)
                                        .Select(s => s.Value)
                                        .SingleOrDefault();
            }

            if (_aeoslabelFaceToken != null)
            {
                _logger.Information("AEOS Label {aeoslabelFaceToken} is available and will be used as clientId", _aeoslabelFaceToken);
                _returnedValue = _aeoslabelFaceToken;
                return _returnedValue;
            }
            else if (_aeoslabelFaceToken == null && _aeoslabelQRToken != null)
            {
                _logger.Information("AEOS Label {aeoslabelFaceToken} is not available, but AEOS Label {aeoslabelQRToken} is available and will be used as clientId", _aeoslabelFaceToken, _aeoslabelQRToken);
                _returnedValue = _aeoslabelQRToken;
                return _returnedValue;
            }
            else
            {
                if (_labelStrictUsageMode)
                {
                    _logger.Information("Skipping user resolution for {watchlistMemberId} ({watchlistMemberName}).", notification.WatchlistMemberId, notification.WatchlistMemberDisplayName);
                    return null;
                }
                else
                {
                    _logger.Information("AEOS Label {aeoslabelFaceToken} and {aeoslabelQRToken} are not available, we will check for WatchlistMemberId", _aeoslabelFaceToken, _aeoslabelQRToken);
                    
                    if (notification.WatchlistMemberId != null)
                    {
                        _logger.Information("WatchlistMemberId {WatchlistMemberId} is available and will be used as clientId", 
                            notification.WatchlistMemberId);

                        _returnedValue = notification.WatchlistMemberId;
                    }
                    else
                    {
                        _logger.Error("No valid client ID found - both values for AEOS Label and WatchlistMemberId are either null or empty");
                        throw new ArgumentException("No valid client ID found - both values for Label and WatchlistMemberId are either null or empty");
                    }
                }

            return _returnedValue;
        }

    }
}
}