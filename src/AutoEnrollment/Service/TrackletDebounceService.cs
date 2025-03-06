using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

using Serilog;

using SmartFace.AutoEnrollment.Models;

namespace SmartFace.AutoEnrollment.Service
{
    public class TrackletDebounceService
    {
        private readonly int TRACKLET_TIMEOUT_MS = 5000;

        private readonly ILogger _logger;
        private readonly ExclusiveMemoryCache _exclusiveMemoryCache;
        private readonly ConcurrentDictionary<string, (List<Notification> notifications, StreamConfiguration streamConfig, DateTime timestamp)> _trackletTimestamps = new();

        private readonly Timer _trackletTimer;
        private Func<Notification, StreamConfiguration, Task> _onTimeout;

        public TrackletDebounceService(
            ILogger logger,
            IConfiguration configuration,
            ExclusiveMemoryCache exclusiveMemoryCache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exclusiveMemoryCache = exclusiveMemoryCache ?? throw new ArgumentNullException(nameof(exclusiveMemoryCache));

            var config = configuration.GetSection("Config").Get<Config>();

            TRACKLET_TIMEOUT_MS = config?.TrackletTimeoutMs ?? TRACKLET_TIMEOUT_MS;

            _trackletTimer = new Timer(CheckForTrackletTimeouts, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        public void Enqueue(Notification notification, StreamConfiguration streamConfig)
        {
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(streamConfig);

            _logger.Information($"{nameof(Enqueue)} tracklet {{trackletId}}", notification.TrackletId);

            if (_trackletTimestamps.TryGetValue(notification.TrackletId, out var existing))
            {
                existing.notifications.Add(notification);
                existing.timestamp = DateTime.UtcNow;
            }
            else
            {
                _trackletTimestamps[notification.TrackletId] = (new List<Notification> { notification }, streamConfig, DateTime.UtcNow);
            }
        }

        public void HandleTimeout(Func<Notification, StreamConfiguration, Task> value)
        {
            ArgumentNullException.ThrowIfNull(value);

            _onTimeout = value;
        }

        private void CheckForTrackletTimeouts(object state)
        {
            var now = DateTime.UtcNow;

            foreach (var kvp in _trackletTimestamps)
            {
                if ((now - kvp.Value.timestamp).TotalMilliseconds > TRACKLET_TIMEOUT_MS)
                {
                    HandleTrackletTimeout(kvp.Key, kvp.Value.notifications, kvp.Value.streamConfig, kvp.Value.timestamp);

                    _trackletTimestamps.TryRemove(kvp.Key, out _);
                }
            }
        }

        private void HandleTrackletTimeout(string trackletId, List<Notification> notifications, StreamConfiguration streamConfig, DateTime timestamp)
        {
            _logger.Information($"{nameof(HandleTrackletTimeout)} tracklet {{trackletId}} timed out after {{timeout}}ms with {{count}} notifications.", 
                trackletId, TRACKLET_TIMEOUT_MS, notifications.Count);

            double weightFaceSize = 50;
            double weightYawAngle = 20;
            double weightPitchAngle = 10;
            double weightRollAngle = 10;
            double weightSharpness = 5;
            double weightBrightness = 5;

            double maxFaceSize = notifications.Max(i => i.FaceSize ?? 0);
            double maxYawAngle = notifications.Max(i => Math.Abs(i.YawAngle ?? 0));
            double maxPitchAngle = notifications.Max(i => Math.Abs(i.PitchAngle ?? 0));
            double maxRollAngle = notifications.Max(i => Math.Abs(i.RollAngle ?? 0));
            double maxSharpness = notifications.Max(i => Math.Abs(i.Sharpness ?? 0));
            double maxBrightness = notifications.Max(i => Math.Abs(i.Brightness ?? 0));

            var notification = notifications
                                    .OrderByDescending(w => ((w.FaceSize / maxFaceSize) * weightFaceSize) +
                                                            ((Math.Abs(w.YawAngle ?? 0) / maxYawAngle) * weightYawAngle) +
                                                            ((Math.Abs(w.PitchAngle ?? 0) / maxPitchAngle) * weightPitchAngle) +
                                                            ((Math.Abs(w.RollAngle ?? 0) / maxRollAngle) * weightRollAngle) +
                                                            ((Math.Abs(w.Sharpness ?? 0) / maxSharpness) * weightSharpness) +
                                                            ((Math.Abs(w.Brightness ?? 0) / maxBrightness) * weightBrightness))
                                    .First();
            
            _onTimeout?.Invoke(notification, streamConfig);            
        }
    }
}