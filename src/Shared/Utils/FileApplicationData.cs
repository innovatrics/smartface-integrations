using System;
using System.IO;

namespace Innovatrics.SmartFace.Integrations.Shared.Utils
{
    public static class FileApplicationData
    {
        public const string ORGANIZATION_NAME = "Innovatrics";

        public const string APP_SETTINGS_JSON_FILENAME = "appsettings.json";

        public static string AppDataDirPath(string appName)
        {
            var commonAppDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create);
            return Path.Combine(commonAppDataDirPath, ORGANIZATION_NAME, appName);
        }
    }
}
