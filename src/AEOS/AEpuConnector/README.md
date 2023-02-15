# AEpu Connector
This application connects to the SmartFace AccessController gRPC stream, processes `GRANTED` notifications and sends `Open` requests to AEpu (Nedap - AEOS) Controller.

## Development
To run the application locally, follow these steps
 - open terminal
 - navigate to /src/AEpuConnector
 - run `dotnet run`

 ## Deployment
 To deploy the application, follow these steps
 - open terminal
 - navigate to /src/AEpuConnector
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEpuConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-AEpuConnector:1.0 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-AEpuConnector:1.0 registry.gitlab.com/innovatrics/smartface/integrations-AEpuConnector:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AEpuConnector:1.0`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AEpuConnector:latest`

## Usage
Add the following pattern to an existing docker compose:

```
      
  ...

  AEpuConnector:
    image: ${REGISTRY}integrations-AEpuConnector
    container_name: SFAEpuConnector
    restart: unless-stopped
    env_file: .env.aepu

networks:
  default:
    external:
      name: sf-network

```