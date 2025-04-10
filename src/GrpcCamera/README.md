# gRPC Camera Server
Create a gRPC Server that streams media to SmartFace as a special type of Camera

## Concept
Key concept of the SmartFace Camera service is it acts as a client that connects to the video source. It could be a RTSP stream, video file (over HTTP or from file system) or images served via gRPC stream. 

In generally, **Camera service is a client** and **video source is a server**.

In this case, SmartFace Camera is a gRPC client connected to a gRPC stream and waits for incomming messages with `Frame`. The gRPC stream has to be produced by a gRPC server using our <a href="protos/frame_analysis.proto" >protobuf</a> schema.

### Create a gRPC Server
This project includes all necessary tools for generating required gRPC classes, as configured in `GrpcCamera.csproj` :

```
<ItemGroup>
    <PackageReference Include="Google.Protobuf" Version="3.17.3" />
    <PackageReference Include="Grpc.Net.Client" Version="2.45.0" />
    <PackageReference Include="Grpc" Version="2.45.0" />
    <PackageReference Include="Grpc.Tools" Version="2.45.0">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
</ItemGroup>

<ItemGroup>
    <ProtoBuf Include="Protos\frame_analysis.proto" GrpcService="Server" />
</ItemGroup>
```

Run a simple build with  `dotnet build` and you will get two files generated in *obj/Debug/net8.0/Protos* 

![FrameAnalysis classes](/assets/GrpcCamera/proto-classes.png)

`VideoAnalyticServiceImpl` is a custom implementation of generated scaffolding `VideoAnalyticServiceBase` from protobuf schema.

```
public class VideoAnalyticServiceImpl : Innovatrics.Smartface.VideoAnalyticService.VideoAnalyticServiceBase
{
```

Important logic is happening in the overriden method `GetFrames`. In this sample we are iterating all Jpeg files in a sample folder and uploading them to the gRPC stream. This can be replaced by any real images from whatever source.

```
await responseStream.WriteAsync(new Frame()
{
    FrameTimestampUs = timestampMs * 1000,
    ImageFormat = ImageFormat.EncodedStandard,
    FrameData = Google.Protobuf.ByteString.CopyFrom(imageData)
});
```

`FrameTimestampUs` is timestamp of particular frame in **microseconds** (therefore miliseconds has to be multiplied by thousand). Field is required, must have a growing value as demonstrated in sample.

`ImageFormat` has to be set when sending compressed images (JPG or PNG). By default, SmartFace camera expects image in raw (BMP) format.


### SmartFace Camera
SmartFace Camera connected to a gRPC stream requires some custom configuration in order to bring the best results. Here they are:

![SmartFace Camera config](/assets/GrpcCamera/camera-config.png)

1. Configure source using custom protocol **sfcam**://ip-address-of-the-gRPC-server:port 
2. Set FACE DISCOVERY FREQUENCY and FACE EXTRACTON FREQUENCY to 1ms in order to process each incomming frame

### Known limitations
- When discovery & extraction frequency set to 1ms, an extra attention must be paid when sending images to the gRPC stream. Full processing usually takes from 150ms to 500ms so you should not sent more than 2 images per second per single camera (stream).
  - images that have not been processed will be dropped (no caching and post-processing)
  - 2 images per second includes safe margin. Exact throughput can be calculated as 1000 ms / time_of_full_processing_of_frame ms = throughput
- All images must the have same resolution per camera (stream). You must not stream one image with 1200x800 and shortly 1000x600 afterwards. It will end up with an exception in SmartFace Camera service.