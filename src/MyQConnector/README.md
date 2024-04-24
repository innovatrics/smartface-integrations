# MyQ Connector
This application connects to the SmartFace AccessController gRPC stream, processes `GRANTED` notifications and sends `Open` requests to MyQ Print Server.

## Development
To run the application locally, follow these steps
 - open terminal
 - navigate to /src/MyQConnector
 - run `dotnet run`

 ## Deployment
 To deploy the application, follow these steps
 - open terminal
 - navigate to /src/MyQConnector
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/MyQConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.1 registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:latest`

 ### Deploy to Docker on Arm
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/MyQConnector/arm.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.1-arm .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.1-arm registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:latest-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.1-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:latest-arm`

## Usage
Add the following pattern to an existing docker compose:

```
      
  ...

  MyQConnector:
    image: ${REGISTRY}integrations-myqconnector:0.1 
    container_name: SFMyQConnector
    restart: unless-stopped
    env_file: .env.aepu

networks:
  default:
    external:
      name: sf-network

```