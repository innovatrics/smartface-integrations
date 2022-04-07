using System.IO;
using System.Net;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Network;
using Serilog.Sinks.Network.Formatters;
using ILogger = Serilog.ILogger;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    internal static class LoggingSetupHelper
    {
        internal const string LOG_FILE_NAME = "AccessController";
        internal const string ERROR_LOG_FILE_NAME = "AccessController_Errors";

        private const int LOG_FILE_SIZE_LIMIT_MEGABYTES = 25;
        private const int LOG_FILE_COUNT_LIMIT = 10;

        private const int ERROR_LOG_FILE_SIZE_LIMIT_MEGABYTES = 100;
        private const int ERROR_LOG_FILE_COUNT_LIMIT = 1;

        internal static ILogger SetupBasicLogging()
        {
            var loggingFile = AbsLogFilePathForFile(LOG_FILE_NAME);
            var errorLogFile = AbsLogFilePathForFile(ERROR_LOG_FILE_NAME);

            var logger = new LoggerConfiguration().
                WithBasicConfiguration(loggingFile, errorLogFile)
                .CreateLogger();

            Log.Logger = logger;

            return logger;
        }

        private static LoggerConfiguration WithBasicConfiguration(this LoggerConfiguration loggerConfiguration, string absLogFilePath, string absErrLogFilePath)
        {
            return loggerConfiguration
                .Enrich.FromLogContext()
                .WriteTo.LiterateConsole(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {Message:j} {Properties:j} {NewLine}{Exception}")
                // Send copies of error log events to separate error log file
                .WriteToRollingFile(absLogFilePath, LOG_FILE_SIZE_LIMIT_MEGABYTES, LOG_FILE_COUNT_LIMIT)
                .WriteTo.Logger(lc =>
                {
                    lc.WriteToRollingFile(absErrLogFilePath, ERROR_LOG_FILE_SIZE_LIMIT_MEGABYTES, ERROR_LOG_FILE_COUNT_LIMIT).Filter
                        .ByIncludingOnly(le =>
                            le.Level == LogEventLevel.Error
                            || le.Level == LogEventLevel.Fatal);
                });
        }

        private static string AbsLogFilePathForFile(string fileNameWithoutExtension)
        {
            var absLogFilePath = Path.GetFullPath(Path.Combine(FileApplicationData.AppDataDirPath(), "logs", $"{fileNameWithoutExtension}.log"));
            return absLogFilePath;
        }
    }
}
