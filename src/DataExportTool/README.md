### Publish
Command to publish

```
dotnet publish "DataExportTool.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish\linux-x64

dotnet publish "DataExportTool.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\win-x64
```