# Identification From Folder
Script takes all photo stored in a folder (it does recursive search in all sub-folders) and Search for matches in SmartFace Watchlists

## How to run

1. Download `win-x64.zip` and unzip
2. Navigate to the folder
3. Execute `.\IdentificationFromFolder.exe folder --source "C:\Users\user\Desktop\SamplePictures" --instance "http://some-smartface-server:8098"`
4. Script will aggregate results into a file named `result-yyyy-MM-dd-HH-mm.html` into the `--source` folder

```
PS C:\Users\user\Downloads\win-x64> .\IdentificationFromFolder.exe folder --source "C:\Users\user\Desktop\SamplePictures" --instance "http://some-smartface-server:8098"
```

## Developers

### How to build & publish
Command to publish

```
dotnet publish "IdentificationFromFolder.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish\linux-x64

dotnet publish "IdentificationFromFolder.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\win-x64
```
