using System.Diagnostics;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Telemetry
{
    public static class AccessControlTelemetry
    {
        public const string ServiceName = "access-control-connector";
        public const string ActivitySourceName = "Innovatrics.SmartFace.AccessControlConnector";

        // Span names
        public const string RequestSpanName = "access_control.request";
        public const string ConnectorHandleSpanName = "access_control.connector.handle";
        public const string ExternalCallSpanName = "access_control.external.call";

        // Attribute keys
        public const string ConnectorNameAttribute = "ac.connector.name";
        public const string ConnectorTypeAttribute = "ac.connector.type";
        public const string StreamIdAttribute = "ac.stream.id";
        public const string ErrorTypeAttribute = "ac.error.type";
        public const string ErrorCodeAttribute = "ac.error.code";

        public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    }
}
