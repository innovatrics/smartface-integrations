using Grpc.Core;
using Innovatrics.Smartface;

namespace Innovatrics.SmartFace.Integrations.GrpcCamera.Services;

public class VideoAnalyticServiceImpl : Innovatrics.Smartface.VideoAnalyticService.VideoAnalyticServiceBase
{
    private readonly string sourcePath;

    public VideoAnalyticServiceImpl()
    {
        var rootDirectory = Directory.GetCurrentDirectory();
        this.sourcePath = Path.Combine(rootDirectory, "stream-out");

        Console.WriteLine($"Image source path {sourcePath}");
    }

    public override async Task GetFrames(IAsyncStreamReader<AnalysisResult> requestStream, IServerStreamWriter<Frame> responseStream, ServerCallContext callContext)
    {
        var startedAt = DateTime.UtcNow;

        Console.WriteLine("Connected client");

        var files = Directory.GetFiles(sourcePath, "*.jpg");

        while (!callContext.CancellationToken.IsCancellationRequested)
        {
            foreach (var file in files)
            {
                var timestampMs = (long)Math.Round((DateTime.UtcNow - startedAt).TotalMilliseconds, 0);

                Console.WriteLine($"Writing stream message {file} in {timestampMs} posMs to connected client");

                var imageData = File.ReadAllBytes(file);

                await responseStream.WriteAsync(new Frame()
                {
                    FrameTimestampUs = timestampMs * 1000,
                    ImageFormat = ImageFormat.EncodedStandard,
                    FrameData = Google.Protobuf.ByteString.CopyFrom(imageData)
                });

                Thread.Sleep(6 * 1000);
            }
        }

        Console.WriteLine("Disconnected client");
    }
}
