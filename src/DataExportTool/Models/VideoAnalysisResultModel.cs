using System;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public class VideoAnalysisResultModel
    {
        public string ProbeId { get; set; }
        public string VideoName { get; set; }
        public Guid VideoFaceImageId { get; set; }
        public Guid WatchlistFaceImageId { get; set; }
        public string WatchlistMemberId { get; set; }
        public int Score { get; set; }
        public long VideoTimeStampMs { get; set; }
        public DateTime FaceProcessedAt { get; set; }
        public DateTime MatchCreatedAt { get; set; }
        public long ProbeOrder { get; set; }
    }
}