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
 - `docker build -f src/AEOS/AeosSync/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeossync:0.5 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeossync:0.5 registry.gitlab.com/innovatrics/smartface/integrations-aeossync:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeossync:0.5`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeossync:latest`

### Deploy to Docker on Arm
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEOS/AeosSync/arm.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeossync:0.5-arm .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeossync:0.5-arm registry.gitlab.com/innovatrics/smartface/integrations-aeossync:latest-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeossync:0.5-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeossync:latest-arm`

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
    image: ${REGISTRY}integrations-aeossync:[version]
    container_name: SFAeosSync
    restart: unless-stopped
    env_file: .env.aeos

networks:
  default:
    external:
      name: sf-network

```