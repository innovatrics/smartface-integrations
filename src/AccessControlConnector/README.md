# SF to Fingera Adapter
This application connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and send `Open` request to Fingera Server

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/AccessControlConnector
 - run `dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/AccessControlConnector
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/AccessControlConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-relayconnector:1.3 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-relayconnector:1.3 registry.gitlab.com/innovatrics/smartface/integrations-relayconnector:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-relayconnector:1.3`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-relayconnector:latest`

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

  relayconnector:
    image: ${REGISTRY}integrations-relayconnector
    container_name: SFAccessControlConnector
    restart: unless-stopped
    environment:
      - AccessController__Host=SFAccessController
      - AccessController__Port=80
      - AccessControlMapping__0__StreamId=ec0437ae-7716-4141-99d9-a9b2a4dd2106
      - AccessControlMapping__0__Host=ip-of-the-relay
      - AccessControlMapping__0__Channel=3
      - AccessControlMapping__1__StreamId=d5ff8f40-f900-4492-8ecc-6a2539648964
      - AccessControlMapping__1__Host=ip-of-the-relay
      - AccessControlMapping__1__Channel=3

networks:
  default:
    external:
      name: sf-network

```