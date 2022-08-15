using System;

namespace Innovatrics.SmartFace.Integrations.AccessController.Notifications
{
    public class BlockedNotification : Notification
    {
        public string WatchlistMemberFullName { get; set; }
        public string WatchlistMemberId { get; set; }
        public string WatchlistId { get; set; }
        public string WatchlistFullName { get; set; }
        public long MatchResultScore { get; set; }
    }
}
