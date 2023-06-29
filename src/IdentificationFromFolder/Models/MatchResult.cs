using System;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.IdentificationFromFolder.Models
{
    public class MatchResult
    {
        public float Score { get; set; }
        public string WatchlistMemberId { get; set; }
        public string DisplayName { get; set; }
        public string FullName { get; set; }
        public string WatchlistDisplayName { get; set; }
        public string WatchlistFullName { get; set; }
    }
}