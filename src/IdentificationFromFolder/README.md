### Publish
Command to publish

```
dotnet publish "IdentificationFromFolder.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish\linux-x64

dotnet publish "IdentificationFromFolder.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\win-x64
```

How to run

1. Download `win-x64.zip` and unzip
2. Navigate to the folder
3. Execute `.\IdentificationFromFolder.exe folder --source "C:\Users\user\Desktop\SamplePictures" --instance "http://some-smartface-server:8098"`

```
PS C:\Users\user\Downloads\win-x64> .\IdentificationFromFolder.exe folder --source "C:\Users\user\Desktop\SamplePictures" --instance "http://some-smartface-server:8098"
```