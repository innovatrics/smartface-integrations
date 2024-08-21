# SmartFace Integrations

This repository contains integrations of Innovatrics SmartFace with various products or technologies.
The repository is a combination of both real-world deployed code and samples for demonstration purposes.

## AccessControlConnector [C#]
Integrates SmartFace AccessControl with 3rd party access control systems such as InnerRange Integrity, AXIS A1001 and other low-level controllers.

## BirdWatch [C#]
Sample application for showing the SmartFace object detection capabilities. The application provides you a guide how to listen to SmartFace notifications for objects and how to share the information with the world - in this case the information is sent into Google Spaces/Chat.

## DataExportTool [C#]
Please check <a href="/src/DataExportTool" >the code</a> for more information.

## FaceGate [C#]
Connects to SmartFace AccessController gRPC stream, process `GRANTED` notifications and send `Open` request to FaceGate Server. Please check <a href="/src/FaceGate" >the code</a> for more information.

## Fingera Adapter [C#]
Listens to gRPC access notifications and in case of granted access calls Fingera (3rd party service) for further action.
Check <a href="src/FingeraAdapter" >the code</a>.

## gRPC Camera Server [C#]
Create a sample gRPC Server that can be connected to SmartFace as a special type of Camera. Check <a href="src/GrpcCamera" >README</a> for more info.

## IdentificationFromFolder [C#]
Takes all photos stored in a folder (it does a recursive search in all sub-folders) and Search for matches in SmartFace Watchlists. Please check <a href="/src/IFaceManualCall" >the code</a> for more information.

## IFaceManualCall [C#]
Sample code for using the IFace features manually. 

## MyQ Connector [C#]
The Connector between the SmartFace and the MyQ print queue system. Adding face biometry for accessing printers.

## Notifications Receiver [C#]
Send configurable events from SmartFace (face detection, body detection, face identification, action detection) to the VMS - NX Witness server. Check <a href="src/NotificationsReceiver" >the code</a>.

## NX Witness Connector [C#]
Send configurable events from SmartFace (face detection, body detection, face identification, action detection) to the VMS - NX Witness server. Check <a href="src/NX-witness-connector" >the code</a>.

## Scripts [Various]
In `src/Scripts` folder you may find various smaller scripts with various purposes available over here. 

## Scripts/AddImagesFromFolderToSmartFace [PowerShell]
A sample PowerShell script for registering a Watchlist Member for each image file in the provided folder.

## Scripts/ComparePersonWithWatchlistMember [Python]
A sample Python script for testing a test image against each face added to a Watchlist Member. The results are provided in a log file.

## Scripts/DownloadAllImagesFromTimeRange [Python]
A sample Python script for downloading all images from the system - faces, pedestrians and objects within a set time range. 

## Scripts/GetAllImagesFromPerson [Python]
A sample Python script to get all faces from the Watchlist Member based on the Display Name.

## Scripts/LivenessCheck [Python]
A sample Python script to check a Liveness score for each image in a provided zip file. Please check <a href="/src/Scripts/LivenessCheck" >the code</a> for more information.

## Scripts/PedestrianHeatmap [C#]
A script to generate a heatmap using a camera's pedestrian data. It will allow you to understand where pedestrians move in the view of a camera.

## Scripts/PythonGraphQLSubscription [Python]
A sample Python script to listen to the SmartFace GraphQL subscriptions.

## Scripts/Remove1000WatchlistMembers [PowerShell]
A script to remove 1000 WatchlistMembers from any watchlist, including users that are not linked to any watchlist. Use with caution.

## Shared [C#]
In the `src/Shared` folder you may find several libraries that are pre-built for reusable purposes

### Shared/AccessController [C#]
The gRPC connector to SmartFace AccessController module. Listen to gRPC access notifications. Used in <a href="src/FingeraAdapter" >Fingera Adapter</a>. Visit <a href="src/Shared/AccessController" >the code</a>.

### Shared/ZeroMQ [C#]
The ZeroMQ connector to SmartFace. Listen to ZeroMQ notifications. Basically, a ZeroMQ wrapper. For example, used in <a href="src/NotificationsReceiver" >Notifications Receiver</a> project. Visit <a href="src/Shared/ZeroMQ" >the code</a>.
