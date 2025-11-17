namespace Kone.Api.Client.Clients.Models
{
    public class LiftPositionResponse
    {
        public LiftPositionData data { get; set; }
    }

    public class LiftPositionData
    {
        public const string MIVING_STATE_STOPPED = "STOPPED";
        public const string MIVING_STATE_STANDING = "STANDING";
        public const string MIVING_STATE_STARTING = "STARTING";
        public const string MIVING_STATE_MOVING = "MOVING";
        public const string MIVING_STATE_DECELERATING = "DECELERATING";

        public const string DIRECTION_UP = "UP";
        public const string DIRECTION_DOWN = "DOWN";

        public string time { get; set; }
        public string dir { get; set; } // Moving direction of lift deck (UP/DOWN(
        public string coll { get; set; } // Lift collective direction (UP/DOWN(
        public string moving_state { get; set; }
        public long area { get; set; }
        public long cur { get; set; }
        public long adv { get; set; }
        public bool door { get; set; }
    }
}
