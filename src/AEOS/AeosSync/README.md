# TODO

# SF to AeosSync Adapter
This application connects to the SmartFace AccessController gRPC stream, processes `GRANTED` notifications and sends `Open` requests to AeosSync Server

## Development
To run the application locally, follow these steps
 - open terminal
 - navigate to /src/AeosSync
 - run `dotnet run`

 ## Deployment
 To deploy the application, follow these steps
 - open terminal
 - navigate to /src/AeosSync
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEOS/AeosSync/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeossync:0.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeossync:0.1 registry.gitlab.com/innovatrics/smartface/integrations-aeossync:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeossync:0.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeossync:latest`

## Usage
Add the following pattern to an existing docker compose:

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
    env_file: .env.aeos

networks:
  default:
    external:
      name: sf-network

```