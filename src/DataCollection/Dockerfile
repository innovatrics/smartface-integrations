ARG VERSION=0.0.1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG VERSION
WORKDIR /source

COPY . .
RUN dotnet restore src/DataCollection/SmartFace.DataCollection.csproj
RUN dotnet build -c Release --no-restore -p:Version=$VERSION src/DataCollection/SmartFace.DataCollection.csproj

FROM build AS publish
ARG VERSION
RUN dotnet publish -c Release --no-build -o /build_output -p:Version=$VERSION src/DataCollection/SmartFace.DataCollection.csproj

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS tool
WORKDIR /build_output
COPY --from=publish /build_output .
ENTRYPOINT ["dotnet", "SmartFace.DataCollection.dll"]