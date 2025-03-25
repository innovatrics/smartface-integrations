# DataCollection App
DataCollection App connects to SmartFace GraphQL endpoint and listens to NoMatch notification. Based on defined criteria then data-collection all unmatched people that pass validation criteria into one or many watchlists.

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/DataCollection
 - run `dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/DataCollection
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/DataCollection/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-data-collection:0.1.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-data-collection:0.1.1 registry.gitlab.com/innovatrics/smartface/integrations-data-collection:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-data-collection:0.1.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-data-collection:latest`

- or run `.\src\DataCollection\release.ps1 0.1.1`