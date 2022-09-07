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
- navigate to root of this repo
- run following commands
 - `docker build -f src/FaceGate/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-facegate:1.0 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-facegate:1.0 registry.gitlab.com/innovatrics/smartface/integrations-facegate:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-facegate:1.0`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-facegate:latest`

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

  facegate:
    image: ${REGISTRY}integrations-facegate
    container_name: SFFaceGate
    restart: unless-stopped
    environment:
      - AccessController__Host=SFAccessController
      - AccessController__Port=80
      - FaceGate__Server=192.168.1.25
      - FaceGate__Cameras__0__Source=ec0437ae-7716-4141-99d9-a9b2a4dd2106
      - FaceGate__Cameras__0__Target=your-checkpoint-id
      - FaceGate__Cameras__1__Source=d5ff8f40-f900-4492-8ecc-6a2539648964
      - FaceGate__Cameras__2__Target=your-another-checkpoint-id

networks:
  default:
    external:
      name: sf-network

```