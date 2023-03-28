namespace ChangiDataExport.Models
{
    public record RegisterWatchlistMemberRequest(string Id, RegistrationImageData[] Images, string[] WatchlistIds, FaceDetectorConfig FaceDetectorConfig);
}
