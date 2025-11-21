namespace AccessControlConnector.Connectors.Kone;

public class KoneConfiguration
{
    public bool LogFullStartupDiagnostics { get; set; }
    public bool LogAllWebSocketMessages { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string BuildingId { get; set; }
    public string GroupId { get; set; }
    public string WebSocketEndpoint { get; set; }
}