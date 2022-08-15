# SmartFace Integrations

This repository contains integrations of Innovatrics SmartFace with various products or technologies.
Repository is a combination of both the real world deployed code and the samples for demonstration purposes.

## Fingera Adapter
Listens to gRPC access notifications and in case of granted access call Fingera (3rd party serfice) for further action.
Check <a href="src/FingeraAdapter" >the code</a>.

## NX Witness Connector
Send configurable events from SmartFace (face detection, body detection, face identification, action detection) to the VMS - NX Witness server. Check <a href="src/NX-witness-connector" >the code</a>.

## Notifications Receiver
Send configurable events from SmartFace (face detection, body detection, face identification, action detection) to the VMS - NX Witness server. Check <a href="src/NotificationsReceiver" >the code</a>.

## Shared
In `src/Shared` folder you may find several libraries that are pre-built for reusable purposes

### Shared/AccessController
The gRPC connector to SmartFace AccessController module. Listen to gRPC access notifications. Used in <a href="src/FingeraAdapter" >Fingera Adapter</a>. Visit <a href="src/Shared/AccessController" >the code</a>.

### Shared/ZeroMQ
The ZeroMQ connector to SmartFace. Listen to ZeroMQ notifications. Basically, a ZeroMQ wrapper. For example, used in <a href="src/NotificationsReceiver" >Notifications Receiver</a> project. Visit <a href="src/Shared/ZeroMQ" >the code</a>.