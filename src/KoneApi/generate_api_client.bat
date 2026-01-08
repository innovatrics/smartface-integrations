dotnet tool restore

set openApiFileName="%cd%\Kone.Api.Client\authentication-api-v2.json"

IF NOT EXIST "%openApiFileName%" (
	TYPE NUL > "%openApiFileName%"
)

REM dotnet swagger needs this variable, otherwise it demands .NET 6 SDK
set DOTNET_ROLL_FORWARD=LatestMajor

dotnet nswag run Kone.Api.Client\nswagConfig.nswag /variables:GeneratedOpenApiSchemaJsonPath="%openApiFileName%"
