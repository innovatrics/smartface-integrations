ARG VERSION=0.0.1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG VERSION
WORKDIR /source

COPY . .
RUN dotnet restore src/AEOS/Dashboards/AeosDashboards.csproj
RUN dotnet publish -c Release -o /build_output -p:Version=$VERSION src/AEOS/Dashboards/AeosDashboards.csproj

FROM build AS publish
ARG VERSION
RUN dotnet publish -c Release --no-build -o /build_output -p:Version=$VERSION src/AEOS/Dashboards/AeosDashboards.csproj

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS tool
WORKDIR /build_output
COPY --from=publish /build_output .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 80
ENTRYPOINT ["dotnet", "AeosDashboards.dll"]