# TODO

# SF to AEOSSync Adapter
This application connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and send `Open` request to AEOSSync Server

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/AEOSSync
 - run `dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/AEOSSync
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/AEOSSync/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-AEOSSync:1.0 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-AEOSSync:1.0 registry.gitlab.com/innovatrics/smartface/integrations-AEOSSync:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AEOSSync:1.0`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AEOSSync:latest`

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

  AEOSSync:
    image: ${REGISTRY}integrations-AEOSSync
    container_name: SFAEOSSync
    restart: unless-stopped
    environment:
      - AccessController__Host=SFAccessController
      - AccessController__Port=80
      - AEOSSync__Server=192.168.1.25
      - AEOSSync__Cameras__0__Source=ec0437ae-7716-4141-99d9-a9b2a4dd2106
      - AEOSSync__Cameras__0__Target=your-checkpoint-id
      - AEOSSync__Cameras__1__Source=d5ff8f40-f900-4492-8ecc-6a2539648964
      - AEOSSync__Cameras__1__Target=your-another-checkpoint-id

networks:
  default:
    external:
      name: sf-network

```