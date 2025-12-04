namespace Kone.Api.Client.Clients.Models
{
    public class PingResponse
    {
        public Data data { get; set; }
        public string callType { get; set; }
        public string buildingId { get; set; }
        public string groupId { get; set; }
    }

    public class PingData
    {
        public int version_major { get; set; }
        public int version_minor { get; set; }
        public List<object> action_set { get; set; } = [];
        public List<object> call_types { get; set; } = [];
        public List<object> functions { get; set; } = [];
    }
}
