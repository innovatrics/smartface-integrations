# SmartFace Integrations

This repository contains integrations of Innovatrics SmartFace with various products or technologies.
The repository is a combination of both real-world deployed code and samples for demonstration purposes.

## AEOS [C#]
Integration between the SmartFace and the AEOS Security Management system from NEDAP. Has 2 integration applications. One for synchronization of data between the SmartFace and the AEOS and the other as a connector between the SmartFace and AOES Aepu controller devices.

## BirdWatch [C#]
Sample application for showing the SmartFace object detection capabilities. The application provides you a guide how to listen to SmartFace notifications for objects and how to share the information with the world - in this case the information is sent into Google Spaces/Chat.

## DataExportTool

## FaceGate

## Fingera Adapter [C#]
Listens to gRPC access notifications and in case of granted access call Fingera (3rd party serfice) for further action.
Check <a href="src/FingeraAdapter" >the code</a>.

## gRPC Camera Server [C#]
Create sample gRPC Server that can be connected to SmartFace as special type of Camera. Check <a href="src/GrpcCamera" >README</a> for more info.

## IdentificationFromFolder

## IFaceManualCall

## LivenessCheck [Python]

## Notifications Receiver [C#]
Send configurable events from SmartFace (face detection, body detection, face identification, action detection) to the VMS - NX Witness server. Check <a href="src/NotificationsReceiver" >the code</a>.

## NX Witness Connector [C#]
Send configurable events from SmartFace (face detection, body detection, face identification, action detection) to the VMS - NX Witness server. Check <a href="src/NX-witness-connector" >the code</a>.

## RelayConnector [C#]

## Scripts [Various]

## Scripts/AddImagesFromFolderToSmartFace [PowerShell]

## Scripts/ComparePersonWithWatchlistMember [Python]

## Scripts/DownloadAllImagesFromTimeRange [Python]

## Scripts/GetAllImagesFromPerson [Python]

## Scripts/LivenessCheck [Python]

## Scripts/PythonGraphQLSubscription [Python]

## Shared [C#]
In `src/Shared` folder you may find several libraries that are pre-built for reusable purposes

### Shared/AccessController [C#]
The gRPC connector to SmartFace AccessController module. Listen to gRPC access notifications. Used in <a href="src/FingeraAdapter" >Fingera Adapter</a>. Visit <a href="src/Shared/AccessController" >the code</a>.

### Shared/ZeroMQ [C#]
The ZeroMQ connector to SmartFace. Listen to ZeroMQ notifications. Basically, a ZeroMQ wrapper. For example, used in <a href="src/NotificationsReceiver" >Notifications Receiver</a> project. Visit <a href="src/Shared/ZeroMQ" >the code</a>.