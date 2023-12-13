using System;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages.Models
{
    public record MatchResult
    {
        public string watchlistMemberId { get; init; }
        public string watchlistMemberFullName { get; init; }
    }
}