# StoreNotifications App
StoreNotifications App connects to SmartFace GraphQL endpoint and listens to NoMatch notification. Based on defined criteria then store-notifications all unmatched people that pass validation criteria into one or many watchlists.

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/StoreNotifications
 - run `dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/StoreNotifications
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/StoreNotifications/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-store-notifications:0.1.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-store-notifications:0.1.1 registry.gitlab.com/innovatrics/smartface/integrations-store-notifications:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-store-notifications:0.1.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-store-notifications:latest`

- or run `.\src\StoreNotifications\release.ps1 0.1.1`