# gRPC Camera Server
Create a gRPC Server that streams media to SmartFace as a special type of Camera

## Concept
Key concept of the SmartFace Camera service is it acts as a client that connects to the video source. It could be a RTSP stream, video file (over HTTP or from file system) or images served via gRPC stream. 

In generally, **Camera service is a client** and **video source is a server**.

In this case, SmartFace Camera is a gRPC client connected to a gRPC stream and waits for incomming messages with `Frame`. The gRPC stream has to be produced by a gRPC server using our <a href="protos/frame_analysis.proto" >protobuf</a> schema.

### gRPC Server
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

Run a simple build with  `dotnet build` and you will get two files generated in *obj/Debug/net6.0/Protos* 

![FrameAnalysis classes](/assets/GrpcCamera/proto-classes.png)

`VideoAnalyticServiceImpl` is a custom implementation of generated scaffolding `VideoAnalyticServiceBase` from protobuf schema.

```
public class VideoAnalyticServiceImpl : Innovatrics.Smartface.VideoAnalyticService.VideoAnalyticServiceBase
{
```

Important logic is happening in the overriden method `GetFrames`. In this sample we are iterating all Jpeg files in a sample folder and uploading them to the gRPC stream. This can be replaced by any real images from whatever source.
Few points has to be taken with special care:

1. All images must the have same resolution. You must not stream one image with 1200x800 and shortly 1000x600 afterwards. It will end up with an exception in SmartFace Camera service.
2. `FrameTimestampUs` is timestamp of particular frame in **microseconds** . Field is required, must have a growing value as demonstrated in sample.

### SmartFace Camera
Custom configuration

![SmartFace Camera config](/assets/GrpcCamera/camera-config.png)