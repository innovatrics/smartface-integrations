# GoogleCalendarsConnector

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/GoogleCalendarsConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:0.0.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:0.0.1 registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:0.0.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:latest`

## Usage
Add the following pattern to your existing docker compose, depending on the integrations used. The environment section below lists the main configurable options for the Google Calendars Connector:

```
  ...

  sf-station:
    image: ${REGISTRY}sf-station:${SFS_VERSION}
    container_name: SFStation
    restart: unless-stopped
    ports:
      - 8000:8000
    env_file: .env.sfstation

  google-calendars:
    image: ${REGISTRY}integrations-google-calendars:latest
    restart: unless-stopped
    environment:
      # Google Calendar configuration
      - GoogleCalendar__CredentialsPath=credentials.json
      - GoogleCalendar__TokenPath=token.json
      - GoogleCalendar__TimeZone=Europe/Bratislava
      - GoogleCalendar__MeetingDurationMin=30
      - GoogleCalendar__ApplicationName=Google Calendar API
      - GoogleCalendar__EventsLookbackHours=24
      - GoogleCalendar__EventsLookaheadHours=24
      - GoogleCalendar__ServiceUser=service@company.com

      # Stream group mapping (example for multiple groups)
      - StreamGroupsMapping__0__GroupName=MeetingRoom1
      - StreamGroupsMapping__0__CalendarId=x@resource.calendar.google.com
      - StreamGroupsMapping__1__GroupName=MeetingRoom2
      - StreamGroupsMapping__1__CalendarId=y@resource.calendar.google.com

      # Stream group tracker
      - StreamGroupTracker__IntervalSec=15
      - StreamGroupTracker__MinPedestrians=1
      - StreamGroupTracker__MinFaces=0

      # Config section
      - Config__MaxParallelActionBlocks=4

      # Source GraphQL (if needed)
      - Source__GraphQL__Schema=ws
      - Source__GraphQL__Host=localhost
      - Source__GraphQL__Port=8096
      - Source__GraphQL__Path=graphql

      # Calendar cache (optional)
      - CalendarCache__ExpirationMinutes=30
      - CalendarCache__MaxSize=1000

networks:
  default:
    external:
      name: sf-network
