ARG VERSION=0.0.1

ARG BUILD_PLATFORM="amd64"
ARG RUNTIME_PLAFTORM="arm64"

ARG BUILD_IMAGE="mcr.microsoft.com/dotnet/sdk:8.0-jammy-amd64"
ARG DOTNET_RUNTIME_IMAGE="mcr.microsoft.com/dotnet/aspnet:8.0-jammy-arm64v8"
ARG COMMON_MSBUILD_OPTIONS="-r linux-arm64 -p:Platform=ARM64 -p:Configuration=Release -p:PublishReadyToRun=true"
ARG MSBUILD_BUILD_OPTIONS="--no-self-contained -p:ImportByWildcardBeforeSolution=false -c Release --no-restore -p:Version=${VERSION} ${COMMON_MSBUILD_OPTIONS}"
ARG MSBUILD_PUBLISH_OPTIONS="-c Release -p:Version=${VERSION} ${COMMON_MSBUILD_OPTIONS}"

FROM --platform=${BUILD_PLATFORM} ${BUILD_IMAGE} AS build
ARG COMMON_MSBUILD_OPTIONS
ARG MSBUILD_PUBLISH_OPTIONS
WORKDIR /source

COPY . .
RUN dotnet restore src/AccessControlConnector/AccessControlConnector.csproj
RUN dotnet publish ${MSBUILD_PUBLISH_OPTIONS} -o /build_output src/AccessControlConnector/AccessControlConnector.csproj

FROM --platform=${RUNTIME_PLAFTORM} ${DOTNET_RUNTIME_IMAGE} AS tool
WORKDIR /App
COPY --from=build /build_output .
ENTRYPOINT ["dotnet", "AccessControlConnector.dll"]