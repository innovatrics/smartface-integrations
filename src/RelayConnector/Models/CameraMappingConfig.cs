using System;

namespace Innovatrics.SmartFace.Integrations.RelayConnector.Models
{
    public class CameraMappingConfig
    {
        public Guid StreamId                            { get; set; }
        public string IPAddress                         { get; set; }
        public int Port                                 { get; set; } = 80;
        public int Channel                              { get; set; } = 0;

        public string AuthUsername                      { get; set; }
        public string AuthPassword                      { get; set; }

        public string[] WatchlistExternalIds            { get; set; }
    }
}
