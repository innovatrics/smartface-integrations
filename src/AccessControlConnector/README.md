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
Add following pattern to existing docker compose:

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
      - AccessControlMapping__0__StreamId=ec0437ae-7716-4141-99d9-a9b2a4dd2106
      - AccessControlMapping__0__Host=ip-of-the-relay
      - AccessControlMapping__0__Channel=3
      - AccessControlMapping__1__StreamId=d5ff8f40-f900-4492-8ecc-6a2539648964
      - AccessControlMapping__1__Host=ip-of-the-relay
      - AccessControlMapping__1__Channel=3

networks:
  default:
    external:
      name: sf-network

```


### To define MYQ Connector this is a sample configuration of the Access Control Controller

```
 access-control-connector:
    image: ${REGISTRY}integrations-access-control-connector:latest
    container_name: SFAccessControlConnector
    restart: unless-stopped
    environment:
      - AccessController__Host=SFAccessController
      - AccessController__Port=80
      - AccessControlMapping__0__Type=MYQ_CONNECTOR
      - AccessControlMapping__0__StreamId=a7206eec-46f1-498a-9a4e-c15983a129d1
      - AccessControlMapping__0__myqPrinter=CSJP42700
      - AccessControlMapping__0__UserResolver=WATCHLIST_MEMBER_LABEL_EMAIL
     # MYQ Printer #1
      - AccessControlMapping__0__Type=MYQ_CONNECTOR
      - AccessControlMapping__0__StreamId=a7206eec-46f1-498a-9a4e-c15983a129d1
      - AccessControlMapping__0__myqPrinter=CSJP42700
      - AccessControlMapping__0__UserResolver=WATCHLIST_MEMBER_LABEL_EMAIL
     # General MyQ Configuration
     - MyQConfiguration__clientId=<id your client ID>
     - MyQConfiguration__clientSecret=<add your client secret>
     - MyQConfiguration__scope=users printers
     - MyQConfiguration__loginInfoType=1
     - MyQConfiguration__myQSchema=https
     - MyQConfiguration__MyQHostname=<add your hostname or IP>
     - MyQConfiguration__MyQPort=443
     - MyQConfiguration__SmartFaceURL=http://<SmartFace URL or hostname>:8098
     - MyQConfiguration__BypassSslValidation=true
```