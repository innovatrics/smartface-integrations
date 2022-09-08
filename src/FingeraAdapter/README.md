# SF to Fingera Adapter
This application connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and send `Open` request to Fingera Server

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/FingeraAdapter
 - run `dotnet build; dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/FingeraAdapter
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/FingeraAdapter/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-fingera:1.0 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-fingera:1.0 registry.gitlab.com/innovatrics/smartface/integrations-fingera:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-fingera:1.0`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-fingera:latest`

### Policies
You can specify currently one policy `AllowedTimeWindow` which authorizes open request only within a given time frame. We receive notifications with UTC date, time in policy must also be specified in UTC date (time) 
````
"Policies" : {
    "AllowedTimeWindow" : {
        "Enabled" : true,
        "From" : "03:00",
        "To" : "19:00"
    }
    ...
````

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

  fingeraadapter:
    image: ${REGISTRY}integrations-fingera
    container_name: SFFingera
    restart: unless-stopped
    environment:
      - AccessController__Host=SFAccessController
      - AccessController__Port=80
      - Policies__AllowedTimeWindow__Enabled=true
      - Policies__AllowedTimeWindow__From=03:00
      - Policies__AllowedTimeWindow__To=19:00

networks:
  default:
    external:
      name: sf-network

```