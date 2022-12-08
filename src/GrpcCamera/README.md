# gRPC Camera Server
Create a gRPC Server that streams media to SmartFace as a special type of Camera

## Concept
Key concept of the SmartFace Camera service is it acts as a client that connects to the video source. It could be a RTSP stream, video file (over HTTP or from file system) or images served via gRPC stream. 

In generally, **Camera service is a client** and **video source is a server**.

In this case, SmartFace Camera is a gRPC client connected to a gRPC stream and waits for incomming messages with `Frame`. The gRPC stream has to be produced by a gRPC server using our <a href="protos/frame_analysis.proto" >protobuf</a> schema.

## How to start
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

So, after a simple `dotnet build` you will get two files generated in *obj/Debug/net6.0/Protos*




