using System.IO;
using System.Net;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Network;
using Serilog.Sinks.Network.Formatters;
using ILogger = Serilog.ILogger;

using Innovatrics.SmartFace.Integrations.Shared.Utils;

namespace Innovatrics.SmartFace.Integrations.Shared.Logging
{
    public static class LoggingSetup
    {
        private const int LOG_FILE_SIZE_LIMIT_MEGABYTES = 25;
        private const int LOG_FILE_COUNT_LIMIT = 10;

        private const int ERROR_LOG_FILE_SIZE_LIMIT_MEGABYTES = 100;
        private const int ERROR_LOG_FILE_COUNT_LIMIT = 1;

        private static LoggerConfiguration CreateConfiguration()
        {
            return new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.LiterateConsole(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {Message:j} {Properties:j} {NewLine}{Exception}"
                );
        }

        public static ILogger SetupBasicLogging()
        {
            var logger = CreateConfiguration().CreateLogger();

            Log.Logger = logger;

            return logger;
        }

        public static ILogger SetupBasicLogging(string appName, string logFileName = null, string errorLogFileName = null)
        {
            var loggingFile = AbsLogFilePathForFile(appName, logFileName ?? "main");
            var errorLogFile = AbsLogFilePathForFile(appName, errorLogFileName ?? "error");

            var logger = CreateConfiguration()
                            .WithBasicConfiguration(loggingFile, errorLogFile)
                            .CreateLogger();

            Log.Logger = logger;

            return logger;
        }

        private static LoggerConfiguration WithBasicConfiguration(this LoggerConfiguration loggerConfiguration, string absLogFilePath, string absErrLogFilePath)
        {
            return loggerConfiguration
                .WithRollingFile(absLogFilePath, LOG_FILE_SIZE_LIMIT_MEGABYTES, LOG_FILE_COUNT_LIMIT)
                .WriteTo.Logger(lc =>
                {
                    lc.WithRollingFile(absErrLogFilePath, ERROR_LOG_FILE_SIZE_LIMIT_MEGABYTES, ERROR_LOG_FILE_COUNT_LIMIT).Filter
                        .ByIncludingOnly(le =>
                            le.Level == LogEventLevel.Error
                            || le.Level == LogEventLevel.Fatal);
                });
        }

        private static string AbsLogFilePathForFile(string appName, string fileNameWithoutExtension)
        {
            var absLogFilePath = Path.GetFullPath(Path.Combine(FileApplicationData.AppDataDirPath(appName), "logs", $"{fileNameWithoutExtension}.log"));
            return absLogFilePath;
        }
    }
}
