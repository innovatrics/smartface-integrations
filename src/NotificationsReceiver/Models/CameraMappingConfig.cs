using System;

namespace Innovatrics.SmartFace.Integrations.NotificationsReceiver.Models
{
    public class CameraMappingConfig
    {
        public Guid StreamId                            { get; set; }
        public CameraMappingConfigCamera NXCamera       { get; set; }
    }

    public class CameraMappingConfigCamera
    {
        public string Id                                { get; set; }
        public string Name                              { get; set; }
    }
}
