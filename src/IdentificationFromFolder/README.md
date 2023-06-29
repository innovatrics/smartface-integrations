### Publish
Command to publish

```
dotnet publish "IdentificationFromFolder.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish\linux-x64

dotnet publish "IdentificationFromFolder.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\win-x64
```

How to run

```
dotnet run individuals -i http://10.11.12.13 --format html -mc 0 
```