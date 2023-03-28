using System;
using ChangiDataExport.Commands;

namespace ChangiDataExport.Models
{
    public class VideoAnalysisResultForWriting
    {
        public string VideoName { get; set; }
        public string VideoFaceImageId { get; set; }
        public string WatchlistFaceImageId { get; set; }
        public string WatchlistMemberId { get; set; }
        public int Score { get; set; }
        public long VideoTimeStampMs { get; set; }
        public DateTime FaceProcessedAt { get; set; }
        public DateTime MatchCreatedAt { get; set; }
        public string ProbeId { get; set; }
        public long ProbeOrder { get; set; }

        public static VideoAnalysisResultForWriting FromDbResult(VideoAnalysisResultModel model)
        {
            return new VideoAnalysisResultForWriting
            {
                Score = model.Score,
                VideoName = model.VideoName,
                FaceProcessedAt = model.FaceProcessedAt.ToLocalTime(),
                MatchCreatedAt = model.MatchCreatedAt.ToLocalTime(),
                WatchlistMemberId = model.WatchlistMemberId,
                VideoTimeStampMs = model.VideoTimeStampMs,
                // VideoFaceImageId = CommonLogic.ImageFileName(model.VideoFaceImageId),
                // WatchlistFaceImageId = CommonLogic.ImageFileName(model.WatchlistFaceImageId),
                ProbeId = model.ProbeId,
                ProbeOrder = model.ProbeOrder
            };
        }
    }
}