using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models.Odata
{
    public record Individual
    {
        public Guid Id { get; init; }
        public DateTime? EntranceTime { get; init; }
        public DateTime? ExitTime { get; init; }
        public int? GroupingMetadataId { get; init; }
        public List<Tracklet> Tracklets { get; init; }
        public Face BestFace { get; init; }
    }
}