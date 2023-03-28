namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public record WatchlistMemberUpsertRequest : WatchlistMemberCreateRequest
    {
        public string Id { get; init; }
    }
}
