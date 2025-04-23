using System;

namespace SmartFace.AutoEnrollment.Service.Clients
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

    public class WatchlistMember
    {
        public Guid Id { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string FullName { get; set; }
    }

    public class PageInfo
    {
        public bool HasNextPage { get; set; }
    }
}