using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace Innovatrics.SmartFace.Integrations.Shared.Logging
{
    public static class LoggingSetup
    {
        private static LoggerConfiguration CreateConfiguration(string logFileName, IConfiguration configuration)
        {
            var loggerConfig = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(
                            path: logFileName,
                            restrictedToMinimumLevel: LogEventLevel.Debug,
                            formatProvider: null,
                            fileSizeLimitBytes: (100 * 1024 * 1024),
                            buffered: false,
                            shared: true,
                            rollOnFileSizeLimit: true,
                            retainedFileCountLimit: 10
                        );

            // Only try to read from configuration if it's not null
            if (configuration != null)
            {
                try
                {
                    loggerConfig = loggerConfig.ReadFrom.Configuration(configuration);
                }
                catch
                {
                    // If configuration reading fails, continue with default settings
                }
            }

            return loggerConfig;
        }

        public static ILogger SetupBasicLogging(string logFileName = "app.log", IConfiguration configuration = null)
        {
            var logger = CreateConfiguration(logFileName, configuration).CreateLogger();

            Log.Logger = logger;

            return logger;
        }
    }
}
