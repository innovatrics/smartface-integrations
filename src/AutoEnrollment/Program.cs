using System;
using System.IO;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.Shared.Extensions;
using Innovatrics.SmartFace.Integrations.Shared.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmartFace.AutoEnrollment.NotificationReceivers;
using SmartFace.AutoEnrollment.Service;

namespace SmartFace.AutoEnrollment
{
    public class Program
    {
        public const string LogFileName = "SmartFace.AutoEnrollment.log";
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

            var loggerConfiguration = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Destructure.ToMaximumCollectionCount(100)
                .Destructure.ToMaximumDepth(5)
                .Destructure.ToMaximumStringLength(1000)
                .WithRollingFile(logFilePath, 15, 7)
                .WithConsole();

            var logger = loggerConfiguration.CreateLogger();
            Log.Logger = logger;

            return logger;
        }

        private static void ConfigureServices(IServiceCollection services, ILogger logger)
        {
            services.AddHttpClient();
            services.AddSingleton(logger);

            services.AddSingleton<IGrpcStreamSubscriber, GrpcStreamSubscriber>();
            services.AddSingleton<GrpcStreamSubscriberFactory>();
            services.AddSingleton<GrpcReaderFactory>();

            services.AddSingleton<OAuthService>();
            services.AddSingleton<ExclusiveMemoryCache>();
            services.AddSingleton<DebouncingService>();
            services.AddSingleton<ValidationService>();
            services.AddSingleton<StreamConfigurationService>();
            services.AddSingleton<INotificationSourceFactory, NotificationSourceFactory>();
            services.AddSingleton<AutoEnrollmentService>();

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
                    ConfigureServices(services, logger);
                })
                .UseSerilog()
                .UseSystemd()
                .UseWindowsService()
            ;
        }
    }
}
