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
- Run `docker build -f FingeraAdapter.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-fingera:1.0 .`
- Run `docker push registry.gitlab.com/innovatrics/smartface/integrations-fingera:1.0`

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