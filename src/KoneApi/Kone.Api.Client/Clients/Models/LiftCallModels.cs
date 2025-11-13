namespace Kone.Api.Client.Clients.Models
{
    public class CallTypeRequest
    {
        public const string TypeLiftCallApi = "lift-call-api-v2";
        public const string TypeCommonApi = "common-api";

        public const string CallTypePing = "ping";
        public const string CallTypeConfig = "config";
        public const string CallTypeAction = "action";

        public string type { get; set; }
        public string buildingId { get; set; }
        public string callType { get; set; }
        public string groupId { get; set; }
        public Payload payload { get; set; }
    }

    public class Payload
    {
        public int request_id { get; set; }
        public string time { get; set; }
        public Call call { get; set; }
        public int area { get; set; }
        public int terminal { get; set; }
    }

    public class Call
    {
        public int action { get; set; }

        public int destination { get; set; }

        public int group_size { get; set; } = 1;
    }
}
