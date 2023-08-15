public class MatchResultSubscriptionResult {
    public MatchResult matchResult { get; set; }

    public class MatchResult {
        public string WatchlistMemberFullName { get; set; }
        public string WatchlistMemberDisplayName { get; set; }
    }
}