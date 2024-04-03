using System;

namespace Innovatrics.SmartFace.Integrations.MyQConnectorNamespace.Models
{
    public class MyQMapping
    {
        public string Type                              { get; set; } = "MyQ";
        public Guid StreamId                            { get; set; }
        public string AEpuHostname                      { get; set; }
        public int AEpuPort                             { get; set; } = 80;

        public string[] WatchlistExternalIds            { get; set; }
    }
}
