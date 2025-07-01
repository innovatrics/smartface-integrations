using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AccessController.Notifications
{
    public interface IIdentifiedNotification
    {
        public string WatchlistMemberId { get; set; }
        public string WatchlistId { get; set; }
        public string WatchlistMemberDisplayName { get; set; }
        public KeyValuePair<string, string>[] WatchlistMemberLabels { get; internal set; }
    }
}
