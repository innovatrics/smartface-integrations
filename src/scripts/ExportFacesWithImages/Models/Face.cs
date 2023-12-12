using System;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages.Models
{
    public record Face
    {
        public Guid Id { get; init; }

        public int? TemplateQuality { get; init; }
        public int? Quality { get; init; }

        public DateTime? CreatedAt { get; init; }
        public DateTime? ProcessedAt { get; init; }

        public Guid ImageDataId { get; init; }
        public Guid? StreamId { get; init; }
        public Guid? TrackletId { get; init; }

        public MatchResult[] matchResults { get; init; }
        public Tracklet tracklet { get; init; }

        public Frame Frame { get; init; }

        public float? CropLeftTopX { get; init; }
        public float? CropLeftTopY { get; init; }
        public float? CropRightTopX { get; init; }
        public float? CropRightTopY { get; init; }
        public float? CropLeftBottomX { get; init; }
        public float? CropLeftBottomY { get; init; }
        public float? CropRightBottomX { get; init; }
        public float? CropRightBottomY { get; init; }
    }
}
