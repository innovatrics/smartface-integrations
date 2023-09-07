using System;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages.Models
{
    public record MatchResult
    {
        public Guid watchlistMemberId { get; init; }
        public string watchlistMemberFullName { get; init; }
    }
}