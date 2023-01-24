# SF to Fingera Adapter
This application connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and sends `Open` request to AEOS (Nedap) Server

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/AEOSConnector
 - run `dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/AEOSConnector
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/AOESConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:1.0 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:1.0 registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:1.0`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:latest`

## Usage
Add following pattern to existing docker compose:

```
      
  ...

  AOESconnector:
    image: ${REGISTRY}integrations-AOESconnector
    container_name: SFAOESConnector
    restart: unless-stopped

networks:
  default:
    external:
      name: sf-network

```