namespace Kone.Api.Client.Clients.Models
{
    public class TopologyResponse
    {
        public string requestId { get; set; }
        public string callType { get; set; }
        public string buildingId { get; set; }
        public string groupId { get; set; }
        public TopologyData data { get; set; }
    }

    public class TopologyData
    {
        public int version_major { get; set; }
        public int version_minor { get; set; }
        public List<Group> groups { get; set; } = [];
        public List<Destination> destinations { get; set; } = [];
        public List<Terminal> terminals { get; set; } = [];
    }

    public class Group
    {
        public int group_id { get; set; }
        public List<int> terminals { get; set; } = [];
        public List<Lift> lifts { get; set; } = [];
    }

    public class Lift
    {
        public int lift_id { get; set; }
        public string lift_name { get; set; }
        public List<DeckData> decks { get; set; } = [];
        public List<Floor> floors { get; set; } = [];
    }

    public class DeckData
    {
        public int deck { get; set; }
        public int area_id { get; set; }
        public List<int> doors { get; set; } = [];
        public List<int> terminals { get; set; } = [];
    }

    public class Floor
    {
        public int lift_floor_id { get; set; }
        public int group_floor_id { get; set; }
        public int height { get; set; }
        public List<Side> sides { get; set; } = [];
    }

    public class Side
    {
        public int lift_side { get; set; }
        public int group_side { get; set; }
        public List<int> decks { get; set; } = [];
    }

    public class Destination
    {
        public int area_id { get; set; }
        public int group_floor_id { get; set; }
        public int group_side { get; set; }
        public string short_name { get; set; }
        public bool exit { get; set; } = false;
        public List<int> terminals { get; set; } = [];
    }

    public class Terminal
    {
        public int terminal_id { get; set; }
        public string type { get; set; }
    }
}
