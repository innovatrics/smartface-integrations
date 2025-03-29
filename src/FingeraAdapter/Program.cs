using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.Shared.Logging;
using Innovatrics.SmartFace.Integrations.Shared.Extensions;

namespace Innovatrics.SmartFace.Integrations.FingeraAdapter
{
    public class Program
    {
        public const string LOG_FILE_NAME = "SmartFace.Integrations.Fingera.log";
        public const string JSON_CONFIG_FILE_NAME = "appsettings.json";

        private static void Main(string[] args)
        {
            try
            {
                var configurationRoot = ConfigureBuilder(args);

                var logger = ConfigureLogger(args, configurationRoot);

                Log.Information("Starting up.");

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
            var logDir = Path.Combine(Path.Combine(commonAppDataDirPath, "Innovatrics", "SmartFace2Fingera"));
            logDir = configuration.GetValue<string>("Serilog:LogDirectory", logDir);            
            var logFilePath = System.IO.Path.Combine(logDir, LOG_FILE_NAME);

            var logger = LoggingSetup.SetupBasicLogging(logFilePath, configuration);

            return logger;
        }

        private static IServiceCollection ConfigureServices(IServiceCollection services, ILogger logger)
        {
            services.AddHttpClient();

            services.AddSingleton<ILogger>(logger);

            services.AddSingleton<IGrpcStreamSubscriber, GrpcStreamSubscriber>();
            services.AddSingleton<GrpcStreamSubscriberFactory>();
            services.AddSingleton<GrpcReaderFactory>();

            services.AddSingleton<IFingeraAdapter, FingeraAdapter>();
            services.AddSingleton<IBridge, Bridge>();

            services.AddHostedService<MainHostedService>();

            return services;
        }

        private static IConfigurationRoot ConfigureBuilder(string[] args)
        {
            return new ConfigurationBuilder()
                    .SetMainModuleBasePath()
                    .AddJsonFile(JSON_CONFIG_FILE_NAME, optional: false)
                    .AddEnvironmentVariables()
                    .AddEnvironmentVariables($"SF_INT_FINGERA_")
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
                    ConfigureServices(services, logger);
                })
                .UseSerilog(logger)
                .UseSystemd()
                .UseWindowsService()
            ;
        }

    }
}
