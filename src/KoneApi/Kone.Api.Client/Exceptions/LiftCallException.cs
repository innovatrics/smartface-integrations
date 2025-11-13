namespace Kone.Api.Client.Exceptions
{
    public class LiftCallException : Exception
    {
        public string JsonMessage { get; }

        public LiftCallException(string message, string jsonMessage) : base($"{message}. Response: {jsonMessage}")
        {
            JsonMessage = jsonMessage;
        }
    }
}
