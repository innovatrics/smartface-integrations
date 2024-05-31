using System;

namespace Innovatrics.SmartFace.Models.API
{
    public class WatchlistMember
    {
        public string Id                        { get; set; }
        public WatchlistMemberLabel[] Labels    { get; set; }
    }
}
