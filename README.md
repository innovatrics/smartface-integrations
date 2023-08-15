# SmartFace Integrations

This repository contains integrations of Innovatrics SmartFace with various products or technologies.
Repository is a combination of both the real world deployed code and the samples for demonstration purposes.

## Relay Connector
Listens to gRPC access notifications and in case of granted access calls relay to close the circuit and open the turnstile. Contains generic `IRelayConnector` in order to be extendable to wide range of relays, currently implemented working sample is <a href="https://www.advantech.com/en-eu/products/da5ad5b2-09b9-418c-9f6a-f4a6e2f8f53a/wise-4060lan/mod_cbf540d5-e152-45ce-8384-f09326ce534f" >Advantech WISE 4060</a>. Check <a href="src/RelayConnector" >the code</a>.

## NX Witness Connector
Receive configurable events from SmartFace (face detection, body detection, face identification, action detection) and forward to the VMS - NX Witness server. Check <a href="src/NX-witness-connector" >the code</a>.

## Notifications Receiver
Receive configurable events from SmartFace (face detection, body detection, face identification, action detection) and log them into the Console. Check <a href="src/NotificationsReceiver" >the code</a>.

## GraphQL Subscriptions sample
Sample code how to create a GraphQL Subscription to Face match event and oudputs to the console. See <a href="src/GraphQLClient" >the code</a>.

## gRPC Camera Server
Create sample gRPC Server that can be connected to SmartFace as special type of Camera. Check <a href="src/GrpcCamera" >README</a> for more info.

## Fingera Adapter
Listens to gRPC access notifications and in case of granted access call Fingera (3rd party serfice) for further action.
Check <a href="src/FingeraAdapter" >the code</a>.

## Shared
In `src/Shared` folder you may find several libraries that are pre-built for reusable purposes

### Shared/AccessController
The gRPC connector to SmartFace AccessController module. Listen to gRPC access notifications. Used in <a href="src/FingeraAdapter" >Fingera Adapter</a>. Visit <a href="src/Shared/AccessController" >the code</a>.

### Shared/ZeroMQ
The ZeroMQ connector to SmartFace. Listen to ZeroMQ notifications. Basically, a ZeroMQ wrapper. For example, used in <a href="src/NotificationsReceiver" >Notifications Receiver</a> project. Visit <a href="src/Shared/ZeroMQ" >the code</a>.
