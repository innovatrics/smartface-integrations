# GoogleCalendarsConnector

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/GoogleCalendarsConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:0.0.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:0.0.1 registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:0.0.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-google-calendars:latest`

## Usage
Add following pattern to existing docker compose, depending on the integrations used:

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
      - GoogleCalendar__MeetingDurationMin=45
      
      - StreamGroupsMapping__0__GroupName=MeetingRoom1
      - StreamGroupsMapping__0__CalendarId=x@resource.calendar.google.com

networks:
  default:
    external:
      name: sf-network

```
