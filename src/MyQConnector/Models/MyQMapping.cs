using System;

namespace Innovatrics.SmartFace.Integrations.MyQConnector.Models
{
    public class MyQMapping
    {
        public string Type                              { get; set; } = "MyQ";
        public Guid StreamId                            { get; set; }
        public string MyQHostname                      { get; set; }
        public int MyQPort                             { get; set; } = 80;
        public string PrinterSn                         { get; set; }

        public string[] WatchlistExternalIds            { get; set; }
    }
}
