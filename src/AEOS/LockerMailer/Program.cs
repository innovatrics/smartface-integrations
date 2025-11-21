using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Net.Http;
using Innovatrics.SmartFace.Integrations.Shared.Logging;
using Innovatrics.SmartFace.Integrations.Shared.Extensions;
using Microsoft.Extensions.Logging;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class Program
    {
        public const string LOG_FILE_NAME = "SmartFace.Integrations.LockerMailer.log";
        public const string JSON_CONFIG_FILE_NAME = "appsettings.json";

        private static readonly HttpClient httpClientSoap = new HttpClient();

        private static void Main(string[] args)
        {
            try
            {
                var configurationRoot = ConfigureBuilder(args);

                var logger = ConfigureLogger(args, configurationRoot);
                Log.Information("======================================================");
                Log.Information("Locker Mailer Starting up.");
                Log.Information("======================================================");

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

        private static Serilog.ILogger ConfigureLogger(string[] args, IConfiguration configuration)
        {
            var commonAppDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create);

            // ReSharper disable once StringLiteralTypo
            var logDir = Path.Combine(Path.Combine(commonAppDataDirPath, "Innovatrics", "SmartFace2LockerMailer"));
            logDir = configuration.GetValue<string>("Serilog:LogDirectory", logDir);            
            var logFilePath = System.IO.Path.Combine(logDir, LOG_FILE_NAME);

            var logger = LoggingSetup.SetupBasicLogging(logFilePath, configuration);

            return logger;
        }

        private static IServiceCollection ConfigureServices(IServiceCollection services, Serilog.ILogger logger, IConfiguration configuration)
        {
            services.AddHttpClient();
            services.AddSmartFaceGraphQLClient()
                            .ConfigureHttpClient((serviceProvider, httpClient) =>
                            {});
            services.AddSingleton<Serilog.ILogger>(logger);
            services.AddSingleton<Services.MailLoggingService>();
            services.AddSingleton<IKeilaDataAdapter, KeilaDataAdapter>();
            services.AddSingleton<IDashboardsDataAdapter, DashBoardsDataAdapter>();
            services.AddSingleton<ISmtpMailAdapter, SmtpMailAdapter>();
            services.AddSingleton<IDataOrchestrator, DataOrchestrator>();
            services.AddHostedService<MainHostedService>();
            services.AddHostedService<Services.AlarmTriggerService>();

            return services;
        }

        private static IConfigurationRoot ConfigureBuilder(string[] args)
        {
            return new ConfigurationBuilder()
                    .SetMainModuleBasePath()
                    .AddJsonFile(JSON_CONFIG_FILE_NAME, optional: false)
                    .AddEnvironmentVariables()
                    .AddEnvironmentVariables($"SF_INT_LockerMailer_")
                    .AddCommandLine(args)
                    .Build();
        }

        public static IHostBuilder CreateHostBuilder(string[] args, Serilog.ILogger logger, IConfigurationRoot configurationRoot)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddConfiguration(configurationRoot);
                })
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services, logger, configurationRoot);
                })
                .UseSerilog()
                .UseSystemd()
                .UseWindowsService();
        }
    }
}
