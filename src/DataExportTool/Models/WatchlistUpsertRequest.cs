namespace ChangiDataExport.Models
{
    public record WatchlistUpsertRequest : WatchlistCreateRequest
    {
        public string Id { get; init; }
    }
}
