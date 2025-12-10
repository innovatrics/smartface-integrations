using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AccessController.Notifications
{
    public class GrantedNotification : Notification
    {
        public string WatchlistMemberExternalId  { get; set; }
        public string WatchlistExternalId { get; set; }
        public string WatchlistMemberDisplayName  { get; set; }
        public string WatchlistMemberId  { get; set; }
        public string WatchlistId  { get; set; }
        public string WatchlistDisplayName { get; set; }
        public long MatchResultScore  { get; set; }
        public KeyValuePair<string, string>[] WatchlistMemberLabels { get; internal set; }
    }
}
