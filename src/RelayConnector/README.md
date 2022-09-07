# SF to Fingera Adapter
This application connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and send `Open` request to Fingera Server

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/RelayConnector
 - run `dotnet build; dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/RelayConnector
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- Run `docker build -f src/RelayConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-relayconnector:1.0 .`
- Run `docker push registry.gitlab.com/innovatrics/smartface/integrations-relayconnector:1.0`