# MyQ Connector
This application connects to the SmartFace AccessController gRPC stream, processes `GRANTED` notifications and sends `Open` requests to MyQ Print Server.

## Development
To run the application locally, follow these steps
 - open terminal
 - navigate to /src/MyQConnector
 - run `dotnet run`

 ## Deployment
 To deploy the application, follow these steps
 - open terminal
 - navigate to /src/MyQConnector
 - run `dotnet publish -c Release -r win10-x64 --self-contained true -p:ReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true`

### Deploy to Docker
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/MyQConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.4 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.4 registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:latest`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.4`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:latest`

 ### Deploy to Docker on Arm
- navigate to the root of this repo
- run the following commands
 - `docker build -f src/MyQConnector/arm.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.3-arm .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.3-arm registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:latest-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:0.3-arm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-myqconnector:latest-arm`

## Usage
**1. Update docker-compose.yml file**
Add the following pattern to an existing docker compose:

```
      
  ...

  MyQConnector:
    image: ${REGISTRY}integrations-myqconnector:0.1 
    container_name: SFMyQConnector
    restart: unless-stopped
    env_file: .env.aepu

networks:
  default:
    external:
      name: sf-network

```

**2. Create new environmental file**
Create/add the file `.env.myq` into the same directory as where the `docker-compose.yml` file is located. Inside the file, switch the provided <values> for your values and credentials.

To get the <clientID> and <clientSecret> values you need to log into your MyQ installation as an administrator. Go into Settings > REST API Apps. There you need to Add a new app. Choose a title, such as SmartFace Integration and add scopes: `users` and `printers`. The <clientID> and <clientSecret> will generated and available to you.

```
# General configuration
MyQConfiguration__clientId=<clientID>
MyQConfiguration__clientSecret=<clientSecret>
MyQConfiguration__scope=users printers
MyQConfiguration__loginInfoType=1
MyQConfiguration__MyQSchema=https
MyQConfiguration__MyQHostname=<myq-server-hostname-or-ip>
MyQConfiguration__MyQPort=8090
MyQConfiguration__SmartFaceURL=<smartface-rest-api-url-and-port>
MyQConfiguration__BypassSslValidation=<true-false>

# Camera to printer mapping
MyQMapping__0__StreamId=<smartface-camera-id>
MyQMapping__0__PrinterSn=<printer-serial-number>
```

**3. Apply changes to docker**
To apply changes above, use the command:
```
docker-compose up -d
```

**4. Ensure the SmartFace configuration**
Ensure the Watchlistmembers have filled out a label called **Email**. For more information about how to set and use labels, please read the SmartFace documentation.

**5. Ensure the MyQ configuration**
Ensure the MyQ users have **sAMAccountName** set as the **Card** and **Personal number** for their MyQ profile. This can be done for example if you have LDAP user synchronization. For more information about the LDAP synchronization, please read the MyQ documentation.