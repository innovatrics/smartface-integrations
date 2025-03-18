using System;
using System.IO;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Serilog;

using Innovatrics.SmartFace.StoreNotifications.Services;
using Innovatrics.SmartFace.StoreNotifications.Data;
using Innovatrics.SmartFace.Integrations.Shared.Extensions;
using Innovatrics.SmartFace.Integrations.Shared.Logging;

namespace Innovatrics.SmartFace.StoreNotifications
{
    public class Program
    {
        public const string LogFileName = "SmartFace.StoreNotifications.log";
        public const string JsonConfigFileName = "appsettings.json";

        private static void Main(string[] args)
        {
            try
            {
                var configurationRoot = ConfigureBuilder(args);

                var logger = ConfigureLogger(configurationRoot);

                Log.Information("Starting up");

                var hostBuilder = CreateHostBuilder(args, logger, configurationRoot);
                using var host = hostBuilder.Build();

                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host failed");
                Log.CloseAndFlush();
                throw;
            }

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

        private static void ConfigureServices(IServiceCollection services, ILogger logger, IConfiguration configuration)
        {
            services.AddHttpClient();
            services.AddSingleton(logger);
            services.AddSingleton(configuration);

            services.AddDbContext<MainDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("MainDatabase"))
            );

            services.AddSingleton<QueueProcessingService>();

            services.AddHostedService<MainHostedService>();
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

        public static IHostBuilder CreateHostBuilder(string[] args, ILogger logger, IConfigurationRoot configurationRoot)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddConfiguration(configurationRoot);
                })
                .ConfigureServices((_, services) =>
                {
                    ConfigureServices(services, logger, configurationRoot);
                })
                .UseSerilog()
                .UseSystemd()
                .UseWindowsService()
            ;
        }
    }
}
