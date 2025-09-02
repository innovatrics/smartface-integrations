# LockerMailer
This application manages triggering events for the Nedap Aeos, gathers data, evaluates it, get's email templates, populates them and sends emails

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
 - `docker build -f src/AEOS/Dashboards/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1 registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest`

### Deploy to Docker on Arm
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEOS/Dashboards/arm.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1-arm .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1-arm registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest-arm`

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

  aeosdashboards:
    image: ${REGISTRY}integrations-aeoslockermailer:[version]
    container_name: SFAeosDashboards
    restart: unless-stopped
    ports:
      - 8030:80
    env_file: .env.aeosdash

networks:
  default:
    external:
      name: sf-network

```

For appsettings.json
```
"AeosDashboards": {
        "Aeos": {
            "Server": {
                "Wsdl": "",
                "PageSize": 100,
                "User": "",
                "Pass": ""
            },
            "RefreshPeriodMs": 60000
        },
        "Web": {
            "WebRefreshPeriodMs": 10000
        }
    }
```

For .env.aeosdash
```
AeosDashboards__Aeos__Server__Wsdl=
AeosDashboards__Aeos__Server__PageSize=100,
AeosDashboards__Aeos__Server__User=
AeosDashboards__Aeos__Server__Pass=
AeosDashboards__Aeos__RefreshPeriodMs=60000
AeosDashboards__Web__WebRefreshPeriodMs=10000
```