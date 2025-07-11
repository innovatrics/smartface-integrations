using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using SmartFace.GoogleCalendarsConnector.Models;
using Serilog;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public class StreamGroupTracker
    {
        private readonly ILogger _logger;

        private readonly TimeSpan _interval;
        private readonly int _minPedestrians;
        private readonly int _minFaces;
        private readonly int _minIdentifications;

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

            _interval = TimeSpan.FromSeconds(configuration.GetValue("StreamGroupTracker:IntervalSec", 15));
            _minPedestrians = configuration.GetValue("StreamGroupTracker:MinPedestrians", 1);
            _minFaces = configuration.GetValue("StreamGroupTracker:MinFaces", 0);
            _minIdentifications = configuration.GetValue("StreamGroupTracker:MinIdentifications", 0);

            _logger?.Information("StreamGroupTracker initialized with interval: {Interval}, MinPedestrians: {MinPedestrians}, MinFaces: {MinFaces}", _interval, _minPedestrians, _minFaces);
        }

        public void OnDataReceived(StreamGroupAggregation aggregation)
        {
            ArgumentNullException.ThrowIfNull(aggregation);

            var now = DateTime.UtcNow;
            var groupName = aggregation.StreamGroupName;

            lock (_lock)
            {
                if (!_history.ContainsKey(groupName))
                {
                    _history[groupName] = new List<AggregationSnapshot>();
                }

                var historyForGroup = _history[groupName];

                var aggregationSnapshot = new AggregationSnapshot
                {
                    Timestamp = now,
                    AveragePedestrians = aggregation.AveragePedestrians,
                    AverageFaces = aggregation.AverageFaces,
                    AverageIdentifications = aggregation.AverageIdentifications
                };

                historyForGroup.Add(aggregationSnapshot);

                _logger?.Debug("Added new snapshot for group {GroupName}: {@Snapshot}", groupName, aggregationSnapshot);

                var removedCount = historyForGroup.RemoveAll(snap => now - snap.Timestamp > _interval);
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

            _logger?.Information("Evaluating group {GroupName}", groupName);
            _logger?.Information("Entries: {@Entries}", entries.Select(s => s.AveragePedestrians));

            double avgPedestrians = entries.Average(e => e.AveragePedestrians);
            double avgFaces = entries.Average(e => e.AverageFaces);
            double avgIdentifications = entries.Average(e => e.AverageIdentifications);

            _logger?.Information(
                "Evaluating group {GroupName}: AvgPedestrians={AvgPedestrians}, AvgFaces={AvgFaces}, AvgIdentifications={AvgIdentifications}",
                groupName, avgPedestrians, avgFaces, avgIdentifications
            );

            if (
                (avgPedestrians >= _minPedestrians && avgPedestrians > 0) ||
                (avgFaces >= _minFaces && avgFaces > 0) ||
                (avgIdentifications >= _minIdentifications && avgIdentifications > 0)
            )
            {
                _logger?.Information("Triggering event for group {GroupName}", groupName
                );

                OnTrigger?.Invoke(groupName);

                _history[groupName].Clear();
            }
        }
    }
}