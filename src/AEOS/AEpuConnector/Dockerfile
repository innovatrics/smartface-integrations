ARG VERSION=0.0.1

FROM mcr.microsoft.com/net/sdk:6.0 AS build
ARG VERSION
WORKDIR /source

COPY . .
RUN dotnet restore src/AEpuConnector/AEpuConnector.csproj
RUN dotnet build -c Release --no-restore -p:Version=$VERSION src/AEpuConnector/AEpuConnector.csproj

FROM build AS publish
ARG VERSION
RUN dotnet publish -c Release --no-build -o /build_output -p:Version=$VERSION src/AEpuConnector/AEpuConnector.csproj

FROM mcr.microsoft.com/net/runtime:6.0 AS tool
WORKDIR /build_output
COPY --from=publish /build_output .
ENTRYPOINT ["dotnet", "AEpuConnector.dll"]