using System.Collections.Generic;

namespace ChangiDataExport.Models
{
    public record WatchlistMembersLinkRequest
    {
        public string WatchlistId { get; init; }

        public ICollection<string> WatchlistMembersIds { get; init; }
    }
}
