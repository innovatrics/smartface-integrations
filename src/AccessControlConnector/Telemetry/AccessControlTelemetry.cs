using System.Diagnostics;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Telemetry
{
    public static class AccessControlTelemetry
    {
        public const string ServiceName = "access-control-connector";
        public const string ActivitySourceName = "Innovatrics.SmartFace.AccessControlConnector";

        // Span names
        public const string GrantedOperationName = "access_control.ganted.process";
        public const string ConnectorHandleOperationName = "access_control.connector.handle";
        public const string ExternalCallSpanName = "access_control.external.call";

        // Attribute keys
        public const string ConnectorNameAttribute = "ac.connector.name";
        public const string ConnectorTypeAttribute = "ac.connector.type";
        public const string StreamIdAttribute = "ac.stream.id";

        public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    }
}
