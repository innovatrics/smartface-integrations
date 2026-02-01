# Aeos Dashboards Adapter
This application connects gathers data and communicates with Nedap and additional Innovatrics' integrations. Has Locker Management UI and allows to assign, unassign and unlock a locker.

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
 - `docker build -f src/AEOS/Dashboards/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:1.2 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:1.2 registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:1.2`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:latest`

### Deploy to Docker on Arm
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEOS/Dashboards/arm.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:1.2-arm .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:1.2-arm registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:latest-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeosdashboards:1.2-arm`
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

  aeosdashboards:
    image: ${REGISTRY}integrations-aeosdashboards:[version]
    container_name: SFAeosDashboards
    restart: unless-stopped
    ports:
      - 8020:80
    env_file: .env.aeosdash

networks:
  default:
    external:
      name: sf-network

```

Sample content for .env.aeosdash
```
AeosDashboards__AllowChanges=true
AeosDashboards__Aeos__Server__Wsdl=https://<host>:8443/aeosws?wsdl
AeosDashboards__Aeos__Server__PageSize=100
AeosDashboards__Aeos__Server__User=
AeosDashboards__Aeos__Server__Pass=
AeosDashboards__Aeos__Integration__SmartFace__IdentifierType=Biometric_employee
AeosDashboards__Aeos__Integration__Sharry__IdField=UUID
AeosDashboards__Aeos__Web__WebRefreshPeriodMs=10000
AeosDashboards__Aeos__RefreshPeriodMs=60000
AeosDashboards__LockerManagement__Enabled=true
AeosDashboards__LockerManagement__GroupConfiguration__0__groupName=6thFloor
AeosDashboards__LockerManagement__GroupConfiguration__0__allowUnlock=true
AeosDashboards__LockerManagement__GroupConfiguration__0__groupLayout__0__row=6-101, 6-102, 6-103, 6-104,6-105, 6-106, 6-107, 6-108, 6-109, 6-110, <spacer>, 6-111, 6-112, <bigspacer>, 6-113, 6-114
AeosDashboards__LockerManagement__GroupConfiguration__0__groupLayout__1__row=6-201, 6-202, 6-203, 6-204,6-205, 6-206, 6-207, 6-208, 6-209, 6-210, <spacer>, 6-211, 6-212, 6-213, 6-214, 6-215
AeosDashboards__LockerManagement__GroupConfiguration__0__groupLayout__2__row=6-301, 6-302, 6-303, 6-304,6-305, 6-306, 6-307, 6-308, 6-309, 6-310, <spacer>, 6-311, 6-312, 6-313, (xl)6-314
AeosDashboards__LockerManagement__GroupConfiguration__0__groupLayout__3__row=6-401, 6-402, 6-403, 6-404,6-405, 6-406, 6-407, 6-408, 6-409, 6-410, (s)6-411, (s)6-412, (s)6-413, (s)6-414, (s)6-415, <smallspacer>, 6-416

```