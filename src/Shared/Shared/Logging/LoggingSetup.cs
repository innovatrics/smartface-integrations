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
            return new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
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
        }

        public static ILogger SetupBasicLogging(string logFileName, IConfiguration configuration)
        {
            var logger = CreateConfiguration(logFileName, configuration).CreateLogger();

            Log.Logger = logger;

            return logger;
        }
    }
}
