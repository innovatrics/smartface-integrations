# LockerMailer
This application manages triggering events for the Nedap Aeos, gathers data, evaluates it, get's email templates, populates them and sends emails

## Development
To run the application locally, follow these steps
 - open terminal
 - navigate to /src/LockerMailer
 - run `dotnet run`

 ## Deployment
 To deploy the application, follow these steps
 - open terminal
 - navigate to /src/LockerMailer
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEOS/LockerMailer/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1 registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest`

### Deploy to Docker on Arm
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEOS/LockerMailer/arm.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1-arm .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1-arm registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:0.1-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest-arm`

## Usage
The application configuration is stored in `appsettings.json`:

```json
{
  "LockerMailer": {
    "Connections": {
      "Dashboards": {
        "Host": "http://<host>>",
        "Port": 8020,
        "User": "",
        "Pass": "",
        "RefreshPeriodMs": 60000
      },
      "Keila": {
        "Host": "http://<host>>",
        "Port": 4000,
        "User": "",
        "Pass": "",
        "ApiKey": ""
      }
    }
  }
}
```

### Configuration Options

  sf-station:
    image: ${REGISTRY}sf-station:${SFS_VERSION}
    container_name: SFStation
    restart: unless-stopped
    ports:
      - 8000:8000
    env_file: .env.sfstation

  aeoslockermailer:
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

### Response Data Structure

The API returns assignment change data in the following format:

```json
{
  "lastCheckTime": "2025-09-02T07:05:47.430Z",
  "currentCheckTime": "2025-09-02T07:05:47.430Z",
  "changes": [
    {
      "lockerId": 0,
      "lockerName": "string",
      "groupName": "string",
      "previousAssignedTo": 0,
      "previousAssignedEmployeeName": "string",
      "previousAssignedEmployeeIdentifier": "string",
      "previousAssignedEmployeeEmail": "string",
      "newAssignedTo": 0,
      "newAssignedEmployeeName": "string",
      "newAssignedEmployeeIdentifier": "string",
      "newAssignedEmployeeEmail": "string",
      "changeTimestamp": "2025-09-02T07:05:47.430Z",
      "changeType": "string"
    }
  ],
  "totalChanges": 0
}
```