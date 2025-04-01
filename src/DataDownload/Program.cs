using System;
using System.IO;
using Innovatrics.SmartFace.DataDownload.Services;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.Shared.Extensions;
using Innovatrics.SmartFace.Integrations.Shared.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.DataDownload.Services;

namespace Innovatrics.SmartFace.DataDownload
{
    public class Program
    {
        private static void Main(string[] args)
        {
            var configurationRoot = ConfigureBuilder(args);

            var logger = ConfigureLogger(configurationRoot);

            Log.Information("Starting up");

            var data =

            Log.Information("Program exited successfully");
            Log.CloseAndFlush();
        }

        private static ILogger ConfigureLogger(IConfiguration configuration)
        {
            var commonAppDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create);

            var logDir = Path.Combine(Path.Combine(commonAppDataDirPath, "Innovatrics", "SmartFace"));
            logDir = configuration.GetValue("Serilog:LogDirectory", logDir);
            var logFilePath = Path.Combine(logDir, LogFileName);

            var logger = LoggingSetup.SetupBasicLogging(logFilePath);

            return logger;
        }

        private static IConfigurationRoot ConfigureBuilder(string[] args)
        {
            return new ConfigurationBuilder()
                    .SetMainModuleBasePath()
                    .AddJsonFile(JsonConfigFileName, optional: false)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();
        }

        private static async Task<List<WatchlistMember>> GetPalmsAllAsync()
        {
            var palms = new List<dynamic>();

            do {
                var palmsPage = await GetPalmsAsync(skip, 1000);
                palms.AddRange(palmsPage.items);
                skip += 1000;
            } while (palmsPage.Length == 1000);

            return palms;
        }

        private async Task<dynamic[]> GetPalmsAsync(int skipValue, int smartFaceSetPageSize)
        {
            var graphQlClient = CreateGraphQlClient();

            var graphQLRequest = new GraphQLRequest
            {
                Query = @"
                query palms($skip: Int, $take: Int, $from: DateTime, $to: DateTime) {
                    genericObjects(
                        skip: $skip
                        take: $take
                        where: { processedAt: { gte: $from, lte: $to }, objectType: { eq: 2 } }
                        order: { processedAt: ASC }
                    ) {
                        items {
                        id
                        createdAt
                        processedAt
                        streamId
                        objectType
                        frame {
                            id
                            imageDataId
                        }
                        }
                        totalCount
                        pageInfo {
                        hasNextPage
                        }
                    }
                }",

                Variables = new
                {
                    skip = skipValue,
                    take = smartFaceSetPageSize,
                    from = new DateTime(2025, 03, 25, 0, 0, 0, DateTimeKind.Utc),
                    to = new DateTime(2025, 03, 25, 23, 59, 59, DateTimeKind.Utc)
                }
            };

            var response = await graphQlClient.SendQueryAsync<dynamic>(graphQLRequest);
            return response.Data;
        }
    }
}
