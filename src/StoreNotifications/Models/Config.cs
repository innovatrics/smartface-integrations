namespace Innovatrics.SmartFace.StoreNotifications.Models
{
    public class Config
    {
        public string DebugOutputFolder { get; set; }
        public int? MaxParallelActionBlocks { get; set; }        
        public string[] WatchlistIds { get; set; }
    }
}
