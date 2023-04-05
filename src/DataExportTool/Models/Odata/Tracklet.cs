using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models.Odata
{
    public record Tracklet
    {
        public Guid Id { get; init; }

        public DateTime? TimeAppeared { get; init; } 
        public DateTime? TimeDisappeared { get; init; } 

        public List<Face> Faces { get; init; }
        public Stream Stream { get; init; }
        public Guid? StreamId { get; init; }
    }
}