﻿using Serilog;
using Serilog.Events;

namespace Innovatrics.SmartFace.Integrations.Shared.Extensions
{
    public static class SerilogExtensions
    {

        public static LoggerConfiguration WriteToRollingFile(this LoggerConfiguration configuration, string fullLogFilePath, int fileSizeLimitMegaBytes, int retainedFileCountLimit)
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
    }
}
