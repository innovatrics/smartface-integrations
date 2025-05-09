﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

using Innovatrics.SmartFace.Integrations.Shared.Logging;

namespace Innovatrics.SmartFace.Integrations.NotificationsReceiver
{
    public class Program
    {
        private const string APP_NAME = "NotificationsReceiver";

        public static CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        public static bool ReadKeyToStop = Environment.UserInteractive;

        public static async Task Main(string[] args)
        {
            try
            {
                var logger =  LoggingSetup.SetupBasicLogging();

                var configurationRoot = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json", optional: false)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();

                
                var hostBuilder = CreateHostBuilder(args, logger, configurationRoot);
                using var host = hostBuilder.Build();

                var hostApplicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

                var applicationTask = host.RunAsync();

                await using (hostApplicationLifetime.ApplicationStarted.Register(() =>
                {
                    Log.Information("Application started.");
                })) { }

                await applicationTask;

                Log.Information("Application stopped.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application start-up failed.");
                Log.CloseAndFlush();
                throw;
            }
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

        public static void ConfigureServices(IServiceCollection services, ILogger logger)
        {
            services.AddHttpClient();
   
            services.AddHostedService<WorkerService>();
        }
    }
}
