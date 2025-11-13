using System.Text.Json;

namespace Kone.Api.Client.Exceptions
{
    public class KoneCallException : Exception
    {
        public string JsonMessage { get; }

        public string Error { get; }

        public KoneCallException(string message, string jsonMessage) :
            base($"{message}. Response: {jsonMessage}")
        {
            JsonMessage = jsonMessage;

            try
            {
                using var doc = JsonDocument.Parse(jsonMessage);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("error", out var errorProperty) &&
                    errorProperty.ValueKind == JsonValueKind.String)
                {
                    Error = errorProperty.GetString() ?? string.Empty;
                }
                else
                {
                    Error = "Unknown error";
                }
            }
            catch
            {
                Error = "Failed to parse error from JSON message";
            }
        }
    }
}
