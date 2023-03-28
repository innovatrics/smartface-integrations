using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public record WatchlistMembersLinkRequest
    {
        public string WatchlistId { get; init; }

        public ICollection<string> WatchlistMembersIds { get; init; }
    }
}
