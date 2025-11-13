namespace Kone.Api.Client.Clients.Models
{
    public class ActionsResponse
    {
        public Data data { get; set; }
        public string requestId { get; set; }
        public string callType { get; set; }
        public string buildingId { get; set; }
        public string groupId { get; set; }
    }

    public class Data
    {
        public int version_major { get; set; }
        public int version_minor { get; set; }
        public List<object> action_set { get; set; }
        public List<CallType> call_types { get; set; }
        public List<object> functions { get; set; }
    }

    public class CallType
    {
        public int action_id { get; set; }
        public string action_type { get; set; }
        public bool direct_allowed { get; set; }
        public bool enabled { get; set; }
        public bool group_call_allowed { get; set; }
        public int key { get; set; }
        public string name { get; set; }
        public bool preferred_allowed { get; set; }
    }
}
