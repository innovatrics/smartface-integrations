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
 - `docker build -f src/AEOS/LockerMailer/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:1.5 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:1.5 registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:1.5`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest`

### Deploy to Docker on Arm
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/AEOS/LockerMailer/arm.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:1.5-arm .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:1.5-arm registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:1.5-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-aeoslockermailer:latest-arm`

## Usage
The application configuration is stored in `appsettings.json`:

```json
{
    "LockerMailer": {
        "DebugMode":"false",
        "Connections": {
            "Dashboards": {
                "_comment": "Host does include http://",
                "Host": "",
                "Port": 8020,
                "User": "",
                "Pass": "",
                "RefreshPeriodMs": 60000
            },
            "Keila": {
                "_comment": "Host does include http://",
                "Host": "",
                "Port": 4000,
                "User": "",
                "Pass": "",
                "ApiKey": "",
                "KeilaRefreshPeriodMs": 600000
            },
            "SmtpMailGateway": {
                "_comment": "Host does NOT include http://",
                "Host": "",
                "Port": 1025,
                "User": "", 
                "Pass": "",
                "FromEmail": "no-reply@innovatrics.com",
                "Type": "smtp"
            }
            
        },
        "Alarms": [
            {
                "AlarmName": "Alarm1",
                "AlarmTime": "13:19"
            },
            {
                "AlarmName": "Alarm2",
                "AlarmTime": "12:07"
            },
            {
                "AlarmName": "Alarm3",
                "AlarmTime": "15:12"
            }
        ],
        "ReceptionistEmails": ["a@a.com", "b@b.com"],
        "Templates":
        [   {
                "templateName":"Food Delivery",
                "templateId":"lockers-flow_1",
                "templateDescription":"Food is received under your name. You are assigned a locker with your food.",
                "templateCheckGroup":"6thFloor",
                "cancelTime":"18:00"

            },
            {
                "templateName":"Courier Item Delivery",
                "templateId":"lockers-flow_2",
                "templateDescription":"Item is received under your name. You are assigned a locker with your item.",
                "templateCheckGroup":"7thFloor"

            },
            {
                "templateName":"Courier Item Reminder",
                "templateId":"lockers-flow_3",
                "templateDescription":"It is 5pm, if you currently have an courier locker assigned.",
                "templateCheckGroup":"7thFloor",
                "templateAlarm":"Alarm1",
                "cancelTime":"17:00"

            },
            {
                "templateName":"Food Locker Released",
                "templateId":"lockers-flow_4",
                "templateDescription":"It is 6pm, your locker was released (autorelease). You don't own the food locker anymore.",
                "templateCheckGroup":"6thFloor",
                "templateAlarm":"Alarm2",
                "cancelTime":"18:00"
            },
            {
                "templateName":"Food Lockers To Be Discarted",
                "templateId":"lockers-flow_5",
                "templateDescription":"It is 6pm, lockers were released. The locker owner does NOT own the food locker anymore. Send an email to receptionist email group.",
                "templateCheckGroup":"6thFloor",
                "templateAlarm":"Alarm2"
            },
            {
                "templateName":"Food Locker Reminder",
                "templateId":"lockers-flow_6"   ,
                "templateDescription":"It is 5pm, you currently have a food locker assigned. This is a reminder to pick up your food with warning that it will be discarded if not picked up.",
                "templateCheckGroup":"6thFloor",
                "templateAlarm":"Alarm3",
                "cancelTime":"15:09"
            },
            {
                "templateName":"Permanent Locker Assigned",
                "templateId":"lockers-flow_7"   ,
                "templateDescription":"You have been assigned new permanent locker. Providing information about this locker.",
                "templateCheckGroups":["7thFloor", "6thFloor"]
            },
            {
                "templateName":"Permanent Locker Unassigned",
                "templateId":"lockers-flow_8"   ,
                "templateDescription":"You have been unassigned from a permanent locker. Providing information about this locker.",
                "templateCheckGroups":["7thFloor", "6thFloor"]
            }
        ]
    }
}
```

### Idle Locker Report (daily unused-locker email)

In addition to the alarm-driven flows above, the service sends a **once-per-day report** of
assigned lockers that have not been opened for a while — e.g. so reception can reclaim unused
changing-room lockers. It reuses the Dashboards data source and the SMTP sender and does **not**
release any lockers.

Configured under `LockerMailer:IdleLockerReport`:

```json
"IdleLockerReport": {
    "Enabled": true,
    "TriggerTime": "09:00",
    "IdleDays": 14,
    "CheckGroups": ["Change Room Gents", "Change Room Ladies"],
    "Recipients": ["reception@biometricshouse.com", "peter.novak@innovatrics.com"]
}
```

| Key | Meaning |
|---|---|
| `Enabled` | Master switch. When `false` the report service does nothing. |
| `TriggerTime` | Local time of day (`HH:mm`) the report is sent, once per day. |
| `IdleDays` | Lockers idle for strictly more than this many days are reported. |
| `CheckGroups` | Locker group names to inspect. |
| `Recipients` | Email addresses the report is sent to. |

Only **assigned** lockers are reported. Each row shows the locker name, the assignee, and how long
since it was last opened (e.g. "2 months 3 days ago"). If no lockers qualify, no email is sent.
Honors the global `LockerMailer:DebugMode` (logs the HTML instead of sending).

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
    container_name: SFAeosLockerMailer
    restart: unless-stopped
    ports:
      - 8030:80
    env_file: .env.lockermailer

networks:
  default:
    external:
      name: sf-network

```
