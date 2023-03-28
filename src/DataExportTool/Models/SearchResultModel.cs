using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public class SearchResultModel
    {
        public List<SearchMatchResult> MatchResults { get; set; }
        public int Quality { get; set; }
    }

    public class SearchMatchResult
    {
        public int Score { get; set; }
        public string WatchlistMemberId { get; set; }
        public string DisplayName { get; set; }
        public string FullName { get; set; }
        public string WatchlistId { get; set; }
    }
}