using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using SmartFace.GoogleCalendarsConnector.Models;
using Serilog;

namespace SmartFace.GoogleCalendarsConnector.Service
{
    public class StreamGroupTracker
    {
        private readonly TimeSpan _interval;
        private readonly int _minPedestrians;
        private readonly int _minFaces;
        private readonly ILogger _logger;

        private readonly Dictionary<string, List<AggregationSnapshot>> _history = new();
        private readonly object _lock = new();

        public event Action<string> OnTrigger;

        public StreamGroupTracker(ILogger logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _interval = TimeSpan.FromMinutes(configuration.GetValue("StreamGroupTracker:IntervalSec", 15));
            _minPedestrians = configuration.GetValue("StreamGroupTracker:MinPedestrians", 1);
            _minFaces = configuration.GetValue("StreamGroupTracker:MinFaces", 0);

            _logger?.Information("StreamGroupTracker initialized with interval: {Interval}, MinPedestrians: {MinPedestrians}, MinFaces: {MinFaces}", _interval, _minPedestrians, _minFaces);
        }

        public void OnDataReceived(string groupName, double avgPedestrians, double avgFaces)
        {
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                if (!_history.ContainsKey(groupName))
                {
                    _history[groupName] = new List<AggregationSnapshot>();
                }

                _history[groupName].Add(new AggregationSnapshot
                {
                    Timestamp = now,
                    AveragePedestrians = (int)avgPedestrians,
                    AverageFaces = (int)avgFaces
                });

                var removedCount = _history[groupName].RemoveAll(snap => now - snap.Timestamp > _interval);
                if (removedCount > 0)
                {
                    _logger?.Debug("Removed {RemovedCount} old snapshots for group {GroupName}", removedCount, groupName);
                }

                Evaluate(groupName);
            }
        }

        private void Evaluate(string groupName)
        {
            var entries = _history[groupName];
            if (!entries.Any())
            {
                return;
            }

            double avgPedestrians = entries.Average(e => e.AveragePedestrians);
            double avgFaces = entries.Average(e => e.AverageFaces);

            _logger?.Debug("Evaluating group {GroupName}: AvgPedestrians={AvgPedestrians}, AvgFaces={AvgFaces}, Thresholds: MinPedestrians={MinPedestrians}, MinFaces={MinFaces}",
                groupName, avgPedestrians, avgFaces, _minPedestrians, _minFaces);

            if (avgPedestrians >= _minPedestrians || avgFaces >= _minFaces)
            {
                _logger?.Information("Triggering event for group {GroupName} - AvgPedestrians: {AvgPedestrians}, AvgFaces: {AvgFaces}",
                    groupName, avgPedestrians, avgFaces);
                OnTrigger?.Invoke(groupName);
                _history[groupName].Clear();
            }
        }

        public void ClearHistory(string groupName)
        {
            lock (_lock)
            {
                if (_history.ContainsKey(groupName))
                {
                    _history[groupName].Clear();
                    _logger?.Debug("Cleared history for group {GroupName}", groupName);
                }
            }
        }

        public void ClearAllHistory()
        {
            lock (_lock)
            {
                _history.Clear();
                _logger?.Debug("Cleared all history");
            }
        }
    }
}