using Innovatrics.Smartface;

namespace Innovatrics.SmartFace.Integrations.AccessController.Notifications
{
    public class BlockedNotification : Notification
    {
        public string WatchlistMemberDisplayName { get; set; }
        public string WatchlistMemberId { get; set; }
        public string WatchlistId { get; set; }
        public string WatchlistDisplayName { get; set; }
        public long MatchResultScore { get; set; }
    }
}
