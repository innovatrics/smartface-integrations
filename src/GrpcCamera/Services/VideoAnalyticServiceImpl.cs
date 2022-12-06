using Grpc.Core;
using Innovatrics.Smartface;

namespace Innovatrics.SmartFace.Integrations.GrpcCamera.Services;

public class VideoAnalyticServiceImpl : Innovatrics.Smartface.VideoAnalyticService.VideoAnalyticServiceBase
{
    public VideoAnalyticServiceImpl()
    {
        Console.WriteLine("init");
    }

    public override async Task GetFrames(IAsyncStreamReader<AnalysisResult> requestStream, IServerStreamWriter<Frame> responseStream, ServerCallContext callContext)
    {
        Console.WriteLine("Connected client");

        var files = Directory.GetFiles(@"C:\Users\lmalik\Downloads\test-data", "*.jpg");

        while (!callContext.CancellationToken.IsCancellationRequested)
        {
            foreach (var file in files)
            {
                Console.WriteLine("Writing stream message to connected client");

                var imageData = File.ReadAllBytes(file);
                var protobufData = Google.Protobuf.ByteString.CopyFrom(imageData);

                await responseStream.WriteAsync(new Frame()
                {
                    ImageFormat = ImageFormat.EncodedStandard,
                    FrameData = protobufData
                }).ConfigureAwait(false);

                Thread.Sleep(1000);
            }
        }

        Console.WriteLine("Disconnected client");
    }
}
