# TODO

# SF to AeosSync Adapter
This application connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and send `Open` request to AeosSync Server

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/AeosSync
 - run `dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/AeosSync
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/AeosSync/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-AeosSync:1.0 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-AeosSync:1.0 registry.gitlab.com/innovatrics/smartface/integrations-AeosSync:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AeosSync:1.0`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AeosSync:latest`

## Usage
Add following pattern to existing docker compose:

```
      
  ...

  sf-station:
    image: ${REGISTRY}sf-station:${SFS_VERSION}
    container_name: SFStation
    restart: unless-stopped
    ports:
      - 8000:8000
    env_file: .env.sfstation

  AeosSync:
    image: ${REGISTRY}integrations-AeosSync
    container_name: SFAeosSync
    restart: unless-stopped

networks:
  default:
    external:
      name: sf-network

```