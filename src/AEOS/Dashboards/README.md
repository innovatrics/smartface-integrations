# Aeos Dashboards Adapter
This application connects gathers data and communicates with Nedap and additional Innovatrics' integrations

## Development
To run the application locally, follow these steps
 - open terminal
 - navigate to /src/Dashboards
 - run `dotnet run`

 ## Deployment
 To deploy the application, follow these steps
 - open terminal
 - navigate to /src/Dashboards
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEOS/AeosSync/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:0.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:0.1 registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:0.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:latest`

### Deploy to Docker on Arm
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEOS/AeosSync/arm.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:0.1-arm .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:0.1-arm registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:latest-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:0.1-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:latest-arm`

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

  AeosDashboards:
    image: ${REGISTRY}integrations-aeosdashboards:[version]
    container_name: SFAeosDashboards
    restart: unless-stopped
    env_file: .env.aeosdash

networks:
  default:
    external:
      name: sf-network

```