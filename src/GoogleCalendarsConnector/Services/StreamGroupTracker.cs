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
        private readonly double _minPedestrians;
        private readonly double _minFaces;
        private readonly double _minIdentifications;

        private readonly Dictionary<string, List<AggregationSnapshot>> _history = new();
        private readonly Dictionary<string, bool> _occupancyStates = new();
        private readonly object _lock = new();
        private readonly Dictionary<string, StreamGroupAggregation> _latestAggregations = new();
        private readonly StreamGroupMapping[] _streamGroupsMapping;

        public event Action<string, bool, IdentificationResult[]?>? OnOccupancyChanged;

        public StreamGroupTracker(ILogger logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _interval = TimeSpan.FromSeconds(configuration.GetValue("StreamGroupTracker:IntervalSec", 15.0));
            _minPedestrians = configuration.GetValue("StreamGroupTracker:MinPedestrians", 1.0);
            _minFaces = configuration.GetValue("StreamGroupTracker:MinFaces", 0.0);
            _minIdentifications = configuration.GetValue("StreamGroupTracker:MinIdentifications", 0.0);

            _logger.Information("StreamGroupTracker initialized with interval: {Interval}, MinPedestrians: {MinPedestrians}, MinFaces: {MinFaces}, MinIdentifications: {MinIdentifications}",
                _interval, _minPedestrians, _minFaces, _minIdentifications);

            _streamGroupsMapping = configuration.GetStreamGroupsMapping();
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

                var snapshot = new AggregationSnapshot
                {
                    Timestamp = now,
                    AveragePedestrians = aggregation.AveragePedestrians,
                    AverageFaces = aggregation.AverageFaces,
                    AverageIdentifications = aggregation.AverageIdentifications
                };

                historyForGroup.Add(snapshot);
                _logger.Debug("Added snapshot for {GroupName}: {@Snapshot}", groupName, snapshot);

                int removed = historyForGroup.RemoveAll(snap => now - snap.Timestamp > _interval);
                if (removed > 0)
                {
                    _logger.Debug("Removed {Count} old snapshots for group {GroupName}", removed, groupName);
                }

                // Store the latest aggregation for identifications
                _latestAggregations[groupName] = aggregation;

                EvaluateOccupancy(groupName);
            }
        }

        private void EvaluateOccupancy(string groupName)
        {
            var streamGroupMapping = _streamGroupsMapping.FirstOrDefault(e => e.GroupName == groupName);

            var historyForGroup = _history[groupName];
            if (!historyForGroup.Any())
                return;

            double avgPedestrians = historyForGroup.Average(e => e.AveragePedestrians);
            double avgFaces = historyForGroup.Average(e => e.AverageFaces);
            double avgIdentifications = historyForGroup.Average(e => e.AverageIdentifications);

            _logger.Information("Evaluating group {GroupName}: AvgPedestrians={AvgPedestrians}, AvgFaces={AvgFaces}, AvgIdentifications={AvgIdentifications}",
                groupName, avgPedestrians, avgFaces, avgIdentifications);

            bool isOccupied =
                (avgPedestrians >= (streamGroupMapping?.AveragePedestriansThreshold ?? _minPedestrians) && avgPedestrians > 0) ||
                (avgFaces >= _minFaces && avgFaces > 0) ||
                (avgIdentifications >= _minIdentifications && avgIdentifications > 0);


            _occupancyStates.TryGetValue(groupName, out bool wasOccupied);

            if (wasOccupied != isOccupied)
            {
                _occupancyStates[groupName] = isOccupied;
                _logger.Information("Occupancy changed for group {GroupName} → {State}", groupName, isOccupied ? "OCCUPIED" : "EMPTY");
                var identifications = _latestAggregations.TryGetValue(groupName, out var agg) ? agg.Identifications?.ToArray() : Array.Empty<IdentificationResult>();
                OnOccupancyChanged?.Invoke(groupName, isOccupied, identifications);
            }
        }
    }
}
