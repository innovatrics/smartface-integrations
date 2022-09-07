ARG VERSION=0.0.1

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
ARG VERSION
WORKDIR /source

COPY . .
RUN dotnet restore src/FingeraAdapter/FingeraAdapter.csproj
RUN dotnet build -c Release --no-restore -p:Version=$VERSION src/FingeraAdapter/FingeraAdapter.csproj

# SFBase
FROM build AS publish
ARG VERSION
RUN dotnet publish -c Release --no-build -o /build_output -p:Version=$VERSION src/FingeraAdapter/FingeraAdapter.csproj

FROM mcr.microsoft.com/dotnet/runtime:3.1 AS tool
WORKDIR /build_output
COPY --from=publish /build_output .
ENTRYPOINT ["dotnet", "FingeraAdapter.dll"]