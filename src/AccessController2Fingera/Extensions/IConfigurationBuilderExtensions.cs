using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace SmartFace.Integrations.Fingera.Extensions
{
    public static class IConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder SetMainModuleBasePath(this IConfigurationBuilder configurationBuilder)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return configurationBuilder;
            }

            var directoryName = GetBasePath();
            Log.Information("Base path set to {BasePath}.", directoryName);
            return configurationBuilder.SetBasePath(directoryName);
        }
        
        public static string GetBasePath()
        {
            using (var processModule = Process.GetCurrentProcess().MainModule
                                       ?? throw new ArgumentException("Failed to resolve main module."))
            {
                var directoryName = Path.GetDirectoryName(processModule.FileName);
                return directoryName;
            }
        }
    }
}
