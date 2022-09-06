using System;

namespace Innovatrics.SmartFace.Integrations.RelayConnector.Models
{
    public class RelayMapping
    {
        public string Type                              { get; set; } = "Advantech WISE-4000";
        public Guid StreamId                            { get; set; }
        public string IPAddress                         { get; set; }
        public int Port                                 { get; set; } = 80;
        public int Channel                              { get; set; } = 0;

        public string Username                          { get; set; }
        public string Password                          { get; set; }

        public string[] WatchlistExternalIds            { get; set; }
    }
}
