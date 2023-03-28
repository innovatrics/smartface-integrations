namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public record WatchlistMemberCreateRequest
    {
        public string DisplayName { get; init; }

        public string FullName { get; init; }

        public string Note { get; init; }
    }
}
