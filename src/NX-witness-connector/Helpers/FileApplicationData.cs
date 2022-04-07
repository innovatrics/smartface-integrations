using System;
using System.IO;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    internal static class FileApplicationData
    {
        internal const string ORGANIZATION_NAME = "Innovatrics";
        internal const string APP_NAME = "AccessController";

        internal const string APP_SETTINGS_JSON_FILENAME = "appsettings.json";
        internal const string OVERRIDE_FILTERS_CONFIGURATION_APP_SETTINGS_JSON_FILENAME = "appsettings.FiltersConfiguration.json";

        internal static string AppDataDirPath()
        {
            var commonAppDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData,
                Environment.SpecialFolderOption.Create);

            return Path.Combine(commonAppDataDirPath, ORGANIZATION_NAME, APP_NAME);
        }
    }
}
