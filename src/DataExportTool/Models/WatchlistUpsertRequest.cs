namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public record WatchlistUpsertRequest : WatchlistCreateRequest
    {
        public string Id { get; init; }
    }
}
