namespace ChangiDataExport.Models
{
    public record WatchlistMemberUpsertRequest : WatchlistMemberCreateRequest
    {
        public string Id { get; init; }
    }
}
