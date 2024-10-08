﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AccessController.Notifications
{
    public class FaceGrantedNotification : Notification
    {
        public string WatchlistMemberExternalId  { get; set; }
        public string WatchlistExternalId { get; set; }
        public string WatchlistMemberFullName  { get; set; }
        public string WatchlistMemberId  { get; set; }
        public string WatchlistId  { get; set; }
        public string WatchlistFullName { get; set; }
        public long MatchResultScore  { get; set; }
        public new DateTime FaceDetectedAt { get; set; }
        public KeyValuePair<string, string>[] WatchlistMemberLabels { get; internal set; }
    }
}
