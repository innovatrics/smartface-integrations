using System;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Models
{
    public class AccessControlMapping
    {
        public string Type                              { get; set; } = "Advantech WISE-4000";
        public Guid StreamId                            { get; set; }
        public string Schema                            { get; set; } = "http";
        public string Host                              { get; set; }
        public int? Port                                { get; set; }

        public string Username                          { get; set; }
        public string Password                          { get; set; }

        public string[] WatchlistExternalIds            { get; set; }
        public string UserResolver                      { get; set; }

        public int? Channel                             { get; set; }
        public string Reader                            { get; set; }
        public string DoorName                          { get; set; }
        public string DoorId                            { get; set; }
        public string Controller                        { get; set; }

        public string Token                             { get; set; }
        public string Params                            { get; set; }
        
        public string Switch                            { get; set; }
        public string Action                            { get; set; }
        
        public int? NextCallDelayMs                     { get; set; }

        public string TargetId                        { get; set; }

        public int? Terminal                           { get; set; }
        public int? Area                               { get; set; }
        

    }
}
