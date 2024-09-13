# AutoEnrollPlugin
This plugin connects to SmartFace GraphQL endpoint and listens to NoMatch notification. Based on defined criteria then auto-enroll all unmatched people that pass validation criteria into one or many watchlists.

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/AutoEnrollPlugin
 - run `dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/AutoEnrollPlugin
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/AutoEnrollPlugin/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-auto-enroll:0.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-auto-enroll:0.1 registry.gitlab.com/innovatrics/smartface/integrations-auto-enroll:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-auto-enroll:0.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-auto-enroll:latest`

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

  auto-enroll:
    image: ${REGISTRY}integrations-auto-enroll
    restart: unless-stopped
    environment:
      - Source__GraphQL__Host=SFGraphQL
      - Source__GraphQL__Port=8097
      - Target__Host=SFApi
      - Target__Port=8098

      - StreamMappings__0__StreamId=ec0437ae-7716-4141-99d9-a9b2a4dd2106
      - StreamMappings__0__WatchlistIds__0=ip-of-the-watchlist
      - StreamMappings__0__FaceQuality__Min=4500
      - StreamMappings__0__FaceSize__Min=70
      - StreamMappings__0__FaceSize__Max=450

      - StreamMappings__1__StreamId=d5ff8f40-f900-4492-8ecc-6a2539648964
      - StreamMappings__1__WatchlistIds__0=ip-of-the-watchlist

networks:
  default:
    external:
      name: sf-network

```