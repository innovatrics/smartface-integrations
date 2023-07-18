using System;

namespace Innovatrics.SmartFace.Integrations.AEpuConnector.Models
{
    public class AEpuMapping
    {
        public string Type                              { get; set; } = "AEpu";
        public Guid StreamId                            { get; set; }
        public string AEpuHostname                      { get; set; }
        public int AEpuPort                             { get; set; } = 80;

        public string[] WatchlistExternalIds            { get; set; }
    }
}
