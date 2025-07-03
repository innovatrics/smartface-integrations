using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AccessController.Notifications
{
    public interface IIdentifiedNotification
    {
        string WatchlistMemberId { get; }
        string WatchlistId { get; }
        string WatchlistMemberDisplayName { get; }
        KeyValuePair<string, string>[] WatchlistMemberLabels { get;  }
    }
}
