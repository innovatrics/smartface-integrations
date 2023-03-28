using System;
using ChangiDataExport.Models.Odata;
using CsvHelper.Configuration.Attributes;

namespace ChangiDataExport.Models
{
    public class FaceResult
    {
        public Guid Id { get; init; }

        public DateTime? CreatedAt { get; init; }
        
        public Guid? CameraId { get; init; }
        public string CameraName { get; internal set; }
        
        [Ignore]
        public byte[] Image { get; internal set; }

        public int? Quality { get; init; }
        public int? TemplateQuality { get; init; }

        public Guid? TrackletId { get; init; }

        public static FaceResult FromDbResult(Face model)
        {
            return new FaceResult
            {
                Id = model.Id,
                CreatedAt = model.CreatedAt,
                CameraId = model.StreamId,
                CameraName = model.Stream?.Name,
                Quality = model.Quality,
                TemplateQuality = model.TemplateQuality,
                TrackletId = model.TrackletId
            };
        }
    }
}