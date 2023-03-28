using System;

namespace ChangiDataExport.Models.Odata
{
    public record Face
    {
        public Guid Id { get; init; }

        public int? TemplateQuality { get; init; }
        public int? Quality { get; init; }
        
        public DateTime? CreatedAt { get; init; }

        public Guid ImageDataId { get; init; }
        public Guid? StreamId { get; init; }
        public Guid? TrackletId { get; init; }

        public Stream Stream { get; init; }
    }
}
