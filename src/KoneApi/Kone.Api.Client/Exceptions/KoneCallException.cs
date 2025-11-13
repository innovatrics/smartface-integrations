namespace Kone.Api.Client.Exceptions
{
    public class KoneCallException : Exception
    {
        public string JsonMessage { get; }

        public KoneCallException(string message, string jsonMessage) : base($"{message}. Response: {jsonMessage}")
        {
            JsonMessage = jsonMessage;
        }
    }
}
