# gRPC Camera Server
Create a gRPC Server that streams media to SmartFace as a special type of Camera

## Concept
Key concept of the SmartFace Camera service is it acts as a client that connects to the video source. It could be a RTSP stream, video file (over HTTP or from file system) or images served via gRPC stream. 

In generally, **Camera service is a client** and **video source is a server**.

In this case, SmartFace Camera is a gRPC client connected to a gRPC stream which waits on incomming messages with `Frame` for analysis. The gRPC stream has to be produced by a gRPC server using our <a href="protos/frame_analysis.proto" >protobuf</a> schema.




