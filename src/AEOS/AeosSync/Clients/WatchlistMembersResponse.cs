namespace Innovatrics.SmartFace.Integrations.AeosSync.Clients
{
    public class WatchlistMembersResponse
    {
        public WatchlistMembers WatchlistMembers { get; set; }
    }

    public class WatchlistMembers
    {
        public WatchlistMember[] Items { get; set; }
        public PageInfo PageInfo { get; set; }
    }

    public class PageInfo
    {
        public bool HasNextPage { get; set; }
    }

    public class WatchlistMember
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string DisplayName { get; set; }
        public string Note { get; set; }
        public Tracklet Tracklet { get; set; }
    }

    public class Tracklet
    {
        public Face[] Faces { get; set; }
    }

    public enum FaceType
    {

        [System.Runtime.Serialization.EnumMember(Value = @"Regular")]
        Regular = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"AutoLearn")]
        AutoLearn = 1,

    }
}