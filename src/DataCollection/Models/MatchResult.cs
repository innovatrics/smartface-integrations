using System;

namespace Innovatrics.SmartFace.DataCollection.Models
{
    public class MatchResult
    {
        public string Id { get; set; }
        public string WatchlistMemberId { get; set; }
        public string WatchlistMemberDisplayName { get; set; }
        public string WatchlistMemberFullName { get; set; }

        public byte[] CropImage { get; set; }

        public double? Score { get; set; }

        public string WatchlistId { get; set; }

        public string StreamId { get; set; }
    }

    public class MatchResultResponse
    {
        public MatchResult MatchResult { get; set; }
    }
}
