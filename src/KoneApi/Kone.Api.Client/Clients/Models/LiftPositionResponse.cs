namespace Kone.Api.Client.Clients.Models
{
    public class LiftPositionResponse
    {
        public LiftPositionData data { get; set; }
    }

    public class LiftPositionData
    {
        public const string MovingStateStopped = "STOPPED";
        public const string MovingStateStanding = "STANDING";
        public const string MovingStateStarting = "STARTING";
        public const string MovingStateMoving = "MOVING";
        public const string MovingStateDecelerating = "DECELERATING";

        public static string[] MovingStates = [
            MovingStateStopped, 
            MovingStateStanding,
            MovingStateStarting,
            MovingStateMoving,
            MovingStateDecelerating,
        ];

        public const string DirectionUp = "UP";
        public const string DirectionDown = "DOWN";
        public const string DirectionNone = "NONE";

        public static string[] Directions = [DirectionUp, DirectionDown, DirectionNone];

        public string time { get; set; }
        public string dir { get; set; } // Moving direction of lift deck (UP/DOWN/NONE)
        public string coll { get; set; } // Lift collective direction (UP/DOWN/NONE)
        public string moving_state { get; set; }
        public int area { get; set; }
        public int cur { get; set; }
        public int adv { get; set; }
        public bool door { get; set; }
    }
}
