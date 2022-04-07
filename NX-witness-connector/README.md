# SF to Fingera Adapter
This application connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and send `Open` request to Fingera Server

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/SmartFace.Integrations.Fingera
 - run `dotnet build; dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/SmartFace.Integrations.Fingera
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

 ## Configuration
 Configuration file `appsettings.json` is placed alongside binaries and holds all configuration.
 
 ### Logs
 Default location where `Log` is placed is `c:\\ProgramData\\Innovatrics\\SmartFace2Fingera` but can be overwritten in `appsettings.json` in
 ````
 "Serilog": {
        "LogDirectory": "c:\\ProgramData\\Innovatrics\\SmartFace2Fingera",
        ...
 ````

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