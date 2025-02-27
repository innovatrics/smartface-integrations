using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace Innovatrics.SmartFace.Integrations.Shared.Logging
{
    public static class LoggingSetup
    {
        private static LoggerConfiguration CreateConfiguration(string logFileName)
        {
            return new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(
                            logFileName,
                            restrictedToMinimumLevel: LogEventLevel.Debug,
                            formatProvider: null,
                            fileSizeLimitBytes: (100 * 1024 * 1024),
                            buffered: false,
                            shared: true,
                            rollOnFileSizeLimit: true,
                            retainedFileCountLimit: 10
                    );
        }

        public static ILogger SetupBasicLogging(string logFileName = "app.log")
        {
            var logger = CreateConfiguration(logFileName).CreateLogger();

            Log.Logger = logger;

            return logger;
        }
    }
}
