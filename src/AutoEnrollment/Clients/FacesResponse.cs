using System;

namespace SmartFace.AutoEnrollment.Service.Clients
{
    public class WatchlistMembersResponse
    {
        public WatchlistMembers Faces { get; set; }
    }

    public class WatchlistMembers
    {
        public WatchlistMember[] Items { get; set; }
    }

    public class WatchlistMember
    {
        public Guid Id { get; set; }
        public string ImageDataId { get; set; }
        public FaceType FaceType { get; set; }
        public string CreatedAt { get; set; }
    }
}