using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages.Models
{
    public record Frame
    {
        public Guid Id { get; init; }
        public Guid? ImageDataId { get; init; }
    }
}