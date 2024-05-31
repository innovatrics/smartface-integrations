using System;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Models
{
    public class AccessControlMapping
    {
        public string Type                              { get; set; } = "Advantech WISE-4000";
        public Guid StreamId                            { get; set; }
        public string Host                              { get; set; }
        public int? Port                                { get; set; }

        public string Username                          { get; set; }
        public string Password                          { get; set; }

        public string[] WatchlistExternalIds            { get; set; }
        public string UserResolver                      { get; set; }

        public int? Channel                             { get; set; }
        public string Reader                            { get; set; }
    }
}
