using System.ComponentModel.DataAnnotations;

namespace SmartFace.Integrations.MockAPI.Models
{
    public class RequestPayload
    {
        [Required]
        public ImageData Image { get; set; }

        public string[] SpoofDetectorResourceIds { get; set; }
    }
}