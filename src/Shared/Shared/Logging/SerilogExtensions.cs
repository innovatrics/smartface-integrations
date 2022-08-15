using Serilog;
using Serilog.Events;

namespace Innovatrics.SmartFace.Integrations.Shared.Logging
{
    public static class SerilogExtensions
    {
        public static LoggerConfiguration WithRollingFile(this LoggerConfiguration configuration, string fullLogFilePath, int fileSizeLimitMegaBytes, int retainedFileCountLimit)
        {
            var cfg = configuration.WriteTo.File(
                fullLogFilePath,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message:lj} {Properties:j} {NewLine}{Exception}",
                formatProvider: null,
                fileSizeLimitBytes: (fileSizeLimitMegaBytes * 1024 * 1024),
                buffered: false,
                shared: true,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: retainedFileCountLimit);

            return cfg;
        }

        public static LoggerConfiguration WithConsole(this LoggerConfiguration configuration)
        {
            var cfg = configuration.WriteTo.LiterateConsole(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {Message:j} {Properties:j} {NewLine}{Exception}");
            return cfg;
        }
    }
}
