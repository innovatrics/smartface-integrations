namespace Kone.Api.Client.Clients.Models
{
    public class LiftCallResponse
    {
        public CallData data { get; set; }
        public List<string> TransitionStates { get; set; }
        public string ResponseMessageRaw { get; set; }
    }

    public class CallData
    {
        public long request_id { get; set; }
        public long session_id { get; set; }
        public bool success { get; set; }
    }
}
