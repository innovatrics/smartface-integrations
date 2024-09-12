using System;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models
{
    public class NoMatchResult
    {
        public string StreamId          { get; set; }
        public byte[] CropImage         { get; set; }        
    }
}
