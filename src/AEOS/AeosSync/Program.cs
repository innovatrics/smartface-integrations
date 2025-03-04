using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Net.Http;
using Innovatrics.SmartFace.Integrations.Shared.Logging;
using Innovatrics.SmartFace.Integrations.Shared.Extensions;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public class Program
    {
        public const string LOG_FILE_NAME = "SmartFace.Integrations.AeosSync.log";
        public const string JSON_CONFIG_FILE_NAME = "appsettings.json";

        private static readonly HttpClient httpClientSoap = new HttpClient();

           private static void Main(string[] args)
        {
            try
            {
                var configurationRoot = ConfigureBuilder(args);

                var logger = ConfigureLogger(args, configurationRoot);

                Log.Information("SmartFace <-> Aeos Synchronization Tool Starting up.");

                var hostBuilder = CreateHostBuilder(args, logger, configurationRoot);
                using var host = hostBuilder.Build();

                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host failed.");
                Log.CloseAndFlush();
                throw;
            }

            Log.Information("Program exited successfully.");
            Log.CloseAndFlush();
        }

        private static ILogger ConfigureLogger(string[] args, IConfiguration configuration)
        {
            var commonAppDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create);

            // ReSharper disable once StringLiteralTypo
            var logDir = Path.Combine(Path.Combine(commonAppDataDirPath, "Innovatrics", "SmartFace2AeosSync"));
            logDir = configuration.GetValue<string>("Serilog:LogDirectory", logDir);            
            var logFilePath = System.IO.Path.Combine(logDir, LOG_FILE_NAME);

            var logger = LoggingSetup.SetupBasicLogging(logFilePath);

            return logger;
        }

        private static IServiceCollection ConfigureServices(IServiceCollection services, ILogger logger, IConfiguration configuration)
        {
            services.AddHttpClient();
            services.AddSmartFaceGraphQLClient()
                        .ConfigureHttpClient((serviceProvider, httpClient) =>
                        {
                            var url = configuration.GetValue<string>("aeossync:SmartFace:GraphQL:ServerUrl");

                            if(url == null)
                            {
                                throw new InvalidOperationException("The SmartFace GraphQL URL is not read.");
                            }
                            httpClient.BaseAddress = new Uri(url);
                        });
            services.AddSingleton<ILogger>(logger);
            services.AddSingleton<ISmartFaceDataAdapter, SmartFaceDataAdapter>();
            services.AddSingleton<IAeosDataAdapter, AeosDataAdapter>();
            services.AddSingleton<IDataOrchestrator, DataOrchestrator>();
            services.AddHostedService<MainHostedService>();

            return services;
        }

        private static IConfigurationRoot ConfigureBuilder(string[] args)
        {
            return new ConfigurationBuilder()
                    .SetMainModuleBasePath()
                    .AddJsonFile(JSON_CONFIG_FILE_NAME, optional: false)
                    .AddEnvironmentVariables()
                    .AddEnvironmentVariables($"SF_INT_AeosSync_")
                    .AddCommandLine(args)
                    .Build();
        }

        public static IHostBuilder CreateHostBuilder(string[] args, ILogger logger, IConfigurationRoot configurationRoot)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddConfiguration(configurationRoot);
                })
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services, logger,configurationRoot);
                })
                .UseSerilog()
                .UseSystemd()
                .UseWindowsService()
            ;
        }

    }
}
