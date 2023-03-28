namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public record FaceDetectorConfig(int MinFaceSize, int MaxFaceSize, int MaxFaces, int ConfidenceThreshold);
}
