namespace Kone.Api.Client.Clients
{
    public class KoneLiftCallException : Exception
    {
        public string JsonMessage { get; }

        public KoneLiftCallException(string message, string jsonMessage) : base($"{message}. Response: {jsonMessage}")
        {
            JsonMessage = jsonMessage;
        }
    }
}
