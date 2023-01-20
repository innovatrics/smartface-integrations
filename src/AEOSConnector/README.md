# SF to Fingera Adapter
This application connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and send `Open` request to Fingera Server

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
 - `docker build -f src/AOESConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:1.3 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:1.3 registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:1.3`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-AOESconnector:latest`

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

  AOESconnector:
    image: ${REGISTRY}integrations-AOESconnector
    container_name: SFAOESConnector
    restart: unless-stopped
    environment:
      - AccessController__Host=SFAccessController
      - AccessController__Port=80
      - AOESMappings__0__StreamId=ec0437ae-7716-4141-99d9-a9b2a4dd2106
      - AOESMappings__0__IpAddress=ip-of-the-AOES
      - AOESMappings__0__Channel=3
      - AOESMappings__1__StreamId=d5ff8f40-f900-4492-8ecc-6a2539648964
      - AOESMappings__1__IpAddress=ip-of-the-AOES
      - AOESMappings__1__Channel=3

networks:
  default:
    external:
      name: sf-network

```