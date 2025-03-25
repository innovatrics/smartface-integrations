ARG VERSION=0.0.1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG VERSION
WORKDIR /source

COPY . .
RUN dotnet restore src/MyQConnector/MyQConnector.csproj
RUN dotnet publish -c Release -o /build_output -p:Version=$VERSION src/MyQConnector/MyQConnector.csproj

# Runtime stage (use ASP.NET Core runtime instead of dotnet/runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /App
COPY --from=build /build_output . 
ENTRYPOINT ["dotnet", "MyQConnector.dll"]
