using System;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.IdentificationFromFolder.Models
{
    public class SearchResult
    {
        public string File { get; set; }
        public MatchResult[] MatchResults { get; set; }
    }
}