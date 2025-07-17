using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Services
{
    public class DebouncedNotificationService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly TimeSpan _debounceWindow;
        private readonly ConcurrentDictionary<string, Timer> _timers = new();
        private readonly ConcurrentDictionary<string, bool> _pendingNotifications = new();
        private readonly object _lock = new();

        public DebouncedNotificationService(ILogger logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));      
            
            var debounceWindowMs = configuration.GetValue<int>("Config:DebounceWindow", 500);
            _debounceWindow = TimeSpan.FromMilliseconds(debounceWindowMs);
        }

        public void RequestNotification(AccessControlMapping mapping, Action<string> onNotify)
        {
            var key = mapping.GroupName ?? mapping.StreamID;
            if (key == null)
            {
                _logger.Warning("AccessControlMapping must have either GroupName or StreamID");
                return;
            }

            _pendingNotifications[key] = true;

            if (_timers.TryGetValue(key, out var existingTimer))
            {
                existingTimer.Change(_debounceWindow, Timeout.InfiniteTimeSpan);
            }
            else
            {
                var timer = new Timer(_ =>
                {
                    if (_pendingNotifications.TryRemove(key, out _))
                    {
                        _logger.Information("Firing debounced notification for {key}", key);
                        onNotify?.Invoke(key);
                    }

                    if (_timers.TryRemove(key, out var t))
                    {
                        t.Dispose();
                    }

                }, null, _debounceWindow, Timeout.InfiniteTimeSpan);

                _timers[key] = timer;
            }
        }

        public void Dispose()
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }

            _timers.Clear();
            _pendingNotifications.Clear();
        }
    }
}