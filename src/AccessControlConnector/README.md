# AccessControlConnector
This application connects SmartFace with range of Access Control system or hardware that can act as an access control device.
Application subscribes to SmartFace AccessController gRPC stream, receive and process `GRANTED` notifications and send `Open` request to Fingera Server

## Development
To run application localy, follow these steps
 - open terminal
 - navigate to /src/AccessControlConnector
 - run `dotnet run`

 ## Deployment
 To deploy application, follow these steps
 - open terminal
 - navigate to /src/AccessControlConnector
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to root of this repo
- run following commands
 - `docker build -f src/AccessControlConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:0.3.1 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:0.3.1 registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:0.3.1`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:latest`

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

  access-control-connector:
    image: ${REGISTRY}integrations-access-control-connector:latest
    container_name: SFAccessControlConnector
    restart: unless-stopped
    environment:
      - AccessController__Host=SFAccessController
      - AccessController__Port=80
      # IP Relay Integration #1
      - AccessControlMapping__0__StreamId=ec0437ae-7716-4141-99d9-a9b2a4dd2106
      - AccessControlMapping__0__Host=ip-of-the-relay
      - AccessControlMapping__0__Channel=3
      # IP Relay Integration #2
      - AccessControlMapping__1__StreamId=d5ff8f40-f900-4492-8ecc-6a2539648964
      - AccessControlMapping__1__Host=ip-of-the-relay
      - AccessControlMapping__1__Channel=3
      # MyQ Printer #1
      - AccessControlMapping__2__Type=MYQ_CONNECTOR
      - AccessControlMapping__2__StreamId=a7206eec-46f1-498a-9a4e-c15983a129d1
      - AccessControlMapping__2__TargetId=CSJP42700
      - AccessControlMapping__2__UserResolver=WATCHLIST_MEMBER_LABEL_EMAIL
      # MyQ Printer #2
      - AccessControlMapping__3__Type=MYQ_CONNECTOR
      - AccessControlMapping__3__StreamId=c74158b8-ede9-432b-fed4-08dd991ee484
      - AccessControlMapping__3__TargetId=WSJP78266
      - AccessControlMapping__3__UserResolver=WATCHLIST_MEMBER_LABEL_EMAIL
      # VillaPro Gate #1 
      - AccessControlMapping__4__Type=VILLA_PRO_CONNECTOR
      - AccessControlMapping__4__StreamId=8821a3dc-fd07-4a53-5e64-08dab1c351a0
      - AccessControlMapping__4__TargetId=000-001
      - AccessControlMapping__4__UserResolver=WATCHLIST_MEMBER_LABEL_TOKEN_VILLAPRO
      # VillaPro Gate #2 
      - AccessControlMapping__5__Type=VILLA_PRO_CONNECTOR
      - AccessControlMapping__5__StreamId=2e49f358-bebb-42b6-03e9-08db6e4030a1
      - AccessControlMapping__5__TargetId=001-575
      - AccessControlMapping__5__UserResolver=WATCHLIST_MEMBER_LABEL_TOKEN_VILLAPRO
      # NEDAP Aeos Controller #1
      - AccessControlMapping__6__Type=AEOS_CONNECTOR 
      - AccessControlMapping__6__StreamId=0195f6a3-aa3c-716e-8116-9c8bbbfa671e
      - AccessControlMapping__6__Host=10.11.109.12
      - AccessControlMapping__6__Port=11020
      - AccessControlMapping__6__UserResolver=AEOS_USER
          # list of allowed Watchlists for the Aeos AEpu controller (optional)
      - AccessControlMapping__6__WatchlistExternalIds__0=2e49f358-bebb-42b6-03e9-08db6e4030a1
      # NEDAP Aeos Controller #2
      - AccessControlMapping__7__Type=AEOS_CONNECTOR 
      - AccessControlMapping__7__StreamId=0195f6a3-aa3c-716e-8116-9c8bbbfa671e
      - AccessControlMapping__7__Host=10.11.109.12
      - AccessControlMapping__7__Port=11020
      - AccessControlMapping__7__UserResolver=AEOS_USER
          # list of allowed Watchlists for the Aeos AEpu controller (optional)
      - AccessControlMapping__7__WatchlistExternalIds__0=2e49f358-bebb-42b6-03e9-08db6e4030a1
      - AccessControlMapping__7__WatchlistExternalIds__1=2e49f358-bebb-42b6-03e9-08db6e4030a1
      # MYQ Integration Configuration (Optional)
      - MyQConfiguration__ClientId=<id your client ID>
      - MyQConfiguration__ClientSecret=<add your client secret>
      - MyQConfiguration__Scope=users printers
      - MyQConfiguration__LoginInfoType=1
      - MyQConfiguration__MyQSchema=https
      - MyQConfiguration__MyQHostname=<add your hostname or IP>
      - MyQConfiguration__MyQPort=443
      - MyQConfiguration__SmartFaceURL=http://<SmartFace URL or hostname>:8098
      - MyQConfiguration__BypassSslValidation=true
       # VillaPro Integration Configuration (Optional)
      - VillaProConfiguration_AuthToken=
      - VillaProConfiguration_SystToken=
      - VillaProConfiguration_BaseUrl=

networks:
  default:
    external:
      name: sf-network

```
