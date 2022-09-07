# SF to FaceGate Adapter
This application connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and send `Open` request to FaceGate Server

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/FaceGate
 - run `dotnet build; dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/FaceGate
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- Run `docker build -f FaceGate.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-facegate:1.0 .`
- Run `docker push registry.gitlab.com/innovatrics/smartface/integrations-facegate:1.0`