ARG VERSION=0.0.1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG VERSION
WORKDIR /source

COPY . .
RUN dotnet restore src/AEOS/AEpuConnector/AEpuConnector.csproj
RUN dotnet publish -c Release -o /build_output -p:Version=$VERSION src/AEOS/AEpuConnector/AEpuConnector.csproj

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS tool
WORKDIR /App
COPY --from=build /build_output .
ENTRYPOINT ["dotnet", "AEpuConnector.dll"]