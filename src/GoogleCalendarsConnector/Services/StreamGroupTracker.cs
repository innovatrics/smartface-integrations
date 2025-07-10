using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public class StreamGroupTracker
    {
        private readonly TimeSpan _window;
        private readonly int _minPedestrians;
        private readonly int _minFaces;
        private readonly Action<string> _onTrigger;

        private readonly Dictionary<string, List<AggregationSnapshot>> _history = new();
        private readonly object _lock = new();

        public StreamGroupTracker(TimeSpan window, int minPedestrians, int minFaces, Action<string> onTrigger)
        {
            _window = window;
            _minPedestrians = minPedestrians;
            _minFaces = minFaces;
            _onTrigger = onTrigger;
        }

        public void OnDataReceived(string groupName, int avgPedestrians, int avgFaces)
        {
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                if (!_history.ContainsKey(groupName))
                    _history[groupName] = new List<AggregationSnapshot>();

                _history[groupName].Add(new AggregationSnapshot
                {
                    Timestamp = now,
                    AveragePedestrians = avgPedestrians,
                    AverageFaces = avgFaces
                });

                _history[groupName].RemoveAll(snap => now - snap.Timestamp > _window);

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

            if (avgPedestrians >= _minPedestrians || avgFaces >= _minFaces)
            {
                _onTrigger(groupName);
                _history[groupName].Clear();
            }
        }
    }
}