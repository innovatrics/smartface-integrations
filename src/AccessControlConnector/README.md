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
- `docker build -f src/AccessControlConnector/Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:1.0.4 .`
- `docker tag registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:1.0.4 registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:latest`
- `docker push registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:1.0.4`
- `docker push registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:latest`

### Deploy to Docker (ARM)
- navigate to root of this repo
- run following commands
 - `docker build -f src/AccessControlConnector/arm.Dockerfile -t registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:0.4arm2 .`
 - `docker tag registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:0.4arm2 registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:latestarm`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:0.4arm2`
 - `docker push registry.gitlab.com/innovatrics/smartface/integrations-access-control-connector:latestarm`


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
      - StreamConfig__0__StreamId=ec0437ae-7716-4141-99d9-a9b2a4dd2106
      - StreamConfig__0__Host=ip-of-the-relay
      - StreamConfig__0__Channel=3
      # IP Relay Integration #2
      - StreamConfig__1__StreamId=d5ff8f40-f900-4492-8ecc-6a2539648964
      - StreamConfig__1__Host=ip-of-the-relay
      - StreamConfig__1__Channel=3
      # MyQ Printer #1
      - StreamConfig__2__Type=MYQ_CONNECTOR
      - StreamConfig__2__StreamId=a7206eec-46f1-498a-9a4e-c15983a129d1
      - StreamConfig__2__TargetId=CSJP42700
      - StreamConfig__2__UserResolver=WATCHLIST_MEMBER_LABEL_EMAIL
      # MyQ Printer #2
      - StreamConfig__3__Type=MYQ_CONNECTOR
      - StreamConfig__3__StreamId=c74158b8-ede9-432b-fed4-08dd991ee484
      - StreamConfig__3__TargetId=WSJP78266
      - StreamConfig__3__UserResolver=WATCHLIST_MEMBER_LABEL_EMAIL
      # VillaPro Gate #1
      - StreamConfig__4__Type=VILLA_PRO_CONNECTOR
      - StreamConfig__4__StreamId=8821a3dc-fd07-4a53-5e64-08dab1c351a0
      - StreamConfig__4__TargetId=000-001
      - StreamConfig__4__UserResolver=WATCHLIST_MEMBER_LABEL_TOKEN_VILLAPRO
      # VillaPro Gate #2
      - StreamConfig__5__Type=VILLA_PRO_CONNECTOR
      - StreamConfig__5__StreamId=2e49f358-bebb-42b6-03e9-08db6e4030a1
      #{deviceId|sysToken}
      - StreamConfig__5__TargetId=001-575|b8942727-c090-4d1d-8bf9-2d6dcc501ea9
      - StreamConfig__5__UserResolver=WATCHLIST_MEMBER_LABEL_TOKEN_VILLAPRO
      # NEDAP Aeos Controller #1
      - StreamConfig__6__Type=AEOS_CONNECTOR
      - StreamConfig__6__StreamId=0195f6a3-aa3c-716e-8116-9c8bbbfa671e
      - StreamConfig__6__Host=10.11.109.12
      - StreamConfig__6__Port=11020
      - StreamConfig__6__UserResolver=AEOS_USER
          # list of allowed Watchlists for the Aeos AEpu controller (optional)
      - StreamConfig__6__WatchlistExternalIds__0=2e49f358-bebb-42b6-03e9-08db6e4030a1
      # NEDAP Aeos Controller #2
      - StreamConfig__7__Type=AEOS_CONNECTOR
      - StreamConfig__7__StreamId=0195f6a3-aa3c-716e-8116-9c8bbbfa671e
      - StreamConfig__7__Host=10.11.109.12
      - StreamConfig__7__Port=11020
      - StreamConfig__7__UserResolver=AEOS_USER
          # list of allowed Watchlists for the Aeos AEpu controller (optional)
      - StreamConfig__7__WatchlistExternalIds__0=2e49f358-bebb-42b6-03e9-08db6e4030a1
      - StreamConfig__7__WatchlistExternalIds__1=2e49f358-bebb-42b6-03e9-08db6e4030a1
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
      - VillaProConfiguration__AuthToken=
      - VillaProConfiguration__BaseUrl=
      # General AEOS NEDAP Configuration
      - AeosConfiguration__LabelName=LOCKERS
      # KONE connector mapping configuration
      - StreamConfig__8__Type=KONE_CONNECTOR
      - StreamConfig__8__StreamId=<add your stream id>
      - StreamConfig__8__DestinationArea=1000
      - StreamConfig__8__IsDirectionUp=true
      # KONE API configuration
      - KoneConfiguration__ClientId=<add your client id>
      - KoneConfiguration__ClientSecret=<add your client secret>
      - KoneConfiguration__BuildingId=${KONE_BUILDING_ID}
      - KoneConfiguration__GroupId=1
      - KoneConfiguration__WebSocketEndpoint=wss://dev.kone.com/stream-v2

networks:
  default:
    external:
      name: sf-network

```

### StreamConfig indexes in environment variables

`StreamConfig__<n>__<Property>` variables are merged by index, property by property, on top of whatever `appsettings.json` inside the image holds at the same index. They do not replace the entry.

Images built before this entry was removed ship a sample entry at index 0:

```json
{ "Enabled": false, "Type": "KONE_CONNECTOR", "StreamId": "00000000-0000-0000-0000-000000000000", "DestinationArea": 1, "IsDirectionUp": false }
```

With such an image, `StreamConfig__0__*` variables overwrite `Type`, `StreamId` and the rest, but `Enabled` stays `false`. The stream is dropped at startup and every GRANTED notification for it logs:

```
Granted grantedNotification for Stream <stream-guid> has no AccessConnector configuration
```

If you see this for a stream you configured at index 0, do one of the following:

- start your `StreamConfig` entries at index 1, or
- add `StreamConfig__0__Enabled=true`, or
- upgrade to an image where `StreamConfig` in `appsettings.json` is empty.

Current images log a warning at startup for every entry that is ignored because `Enabled` is `false`, and list every active entry as `Stream [<stream-guid>] for <Type> ...`. Check these lines after any configuration change.

### KONE Connector

This connector places a landing call through the KONE Elevator Call API v2 (WebSocket) when a SmartFace access GRANTED event occurs on a mapped stream.

1. Global KONE settings (`KoneConfiguration__<Property>` as environment variables):

```json
"KoneConfiguration": {
  "ClientId": "<your-kone-client-id>",
  "ClientSecret": "<your-kone-client-secret>",
  "BuildingId": "<your-building-id>",
  "GroupId": "1",
  "WebSocketEndpoint": "wss://dev.kone.com/stream-v2"
}
```

- ClientId / ClientSecret: credentials from KONE Developer.
- BuildingId: building identifier.
- GroupId: elevator group (default "1").
- WebSocketEndpoint: KONE WebSocket API endpoint.

2. Per-stream mapping (triggers the call on GRANTED for that stream):

```json
"StreamConfig": [
  {
    "Type": "KONE_CONNECTOR",
    "StreamId": "<stream-guid-1>",
    "DestinationArea": 1000,
    "IsDirectionUp": true
  },
  {
    "Type": "KONE_CONNECTOR",
    "StreamId": "<stream-guid-2>",
    "DestinationArea": 2000,
    "IsDirectionUp": false
  }
]
```

- DestinationArea: KONE area ID of the floor the elevator is called to.
- IsDirectionUp: `true` calls the elevator for travel up, `false` for travel down.

When you set the mapping through environment variables, read [StreamConfig indexes in environment variables](#streamconfig-indexes-in-environment-variables) first. A KONE mapping at index 0 is silently disabled on older images.

## OpenTelemetry Tracing

AccessControlConnector supports OpenTelemetry tracing for end-to-end request latency monitoring. This enables you to:

- Measure request lifetime (start → end) inside AccessControlConnector
- Separate "our processing time" from "waiting on external system time"
- Compare connectors and endpoints consistently
- Export traces to standard monitoring tools (Grafana Tempo, Jaeger, Honeycomb, Datadog, etc.)

### Configuration

Tracing is controlled via environment variables (disabled by default):

| Variable | Description | Default |
|----------|-------------|---------|
| `Tracing__Enabled` | Enable Tracing | `false` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector endpoint | `http://localhost:4317` |
| `OTEL_SERVICE_NAME` | Service name in traces | `access-control-connector` |
| `OTEL_TRACES_SAMPLER` | Sampling strategy | SDK default |
| `OTEL_TRACES_SAMPLER_ARG` | Sampler argument (e.g., ratio) | SDK default |
| `DEPLOYMENT_ENVIRONMENT` | Environment tag (dev/staging/prod) | `development` |

### Enabling Tracing

Add the following environment variables to your docker-compose or deployment:

```yaml
access-control-connector:
  image: ${REGISTRY}integrations-access-control-connector:latest
  environment:
    # ... existing configuration ...
    # OpenTelemetry Tracing
    - Tracing__Enabled=true
    - OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
    - OTEL_SERVICE_NAME=access-control-connector
    - OTEL_TRACES_SAMPLER=parentbased_traceidratio
    - OTEL_TRACES_SAMPLER_ARG=0.1
    - DEPLOYMENT_ENVIRONMENT=production
```

### Span Hierarchy

Each request produces the following span hierarchy:

```
access_control.request (root, measures total request time)
├── ac.stream.id: "<stream-guid>"
│
└── access_control.connector.handle (measures connector processing time)
    ├── ac.connector.name: "INNERRANGE_INTEGRITI_22"
    ├── ac.connector.type: "InnerRange"
    │
    └── access_control.external.call (measures external system wait time)
        ├── http.method: "GET"
        ├── http.status_code: 200
        └── http.url: "http://host:port/endpoint"
```

### Span Attributes

| Attribute | Description |
|-----------|-------------|
| `ac.stream.id` | SmartFace stream identifier |
| `ac.connector.name` | Connector type (e.g., INNERRANGE_INTEGRITI_22) |
| `ac.connector.type` | Connector family (e.g., InnerRange, AXIS, KONE) |
| `ac.error.type` | Error type on failures |
| `http.method` | HTTP method for HTTP-based connectors |
| `http.status_code` | HTTP response status code |
| `http.url` | Target URL (sanitized, no credentials) |
| `network.protocol` | Protocol for non-HTTP connectors (tcp, websocket) |

### Testing with Jaeger

To quickly test tracing locally with Jaeger:

```bash
# Start Jaeger all-in-one
docker run -d --name jaeger \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:latest

# Configure AccessControlConnector
export Tracing__Enabled=true
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317

# View traces at http://localhost:16686
```
